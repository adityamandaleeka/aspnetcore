// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#include "processmanager.h"
#include "EventLog.h"
#include "exceptions.h"
#include "SRWSharedLock.h"

std::atomic_bool PROCESS_MANAGER::isWSAStartupDone = false;

PROCESS_MANAGER::PROCESS_MANAGER() :
    m_ppServerProcessList(nullptr)
{
    InitializeSRWLock(&m_srwLock);
}

PROCESS_MANAGER::~PROCESS_MANAGER()
{
}

HRESULT PROCESS_MANAGER::Initialize()
{
    WSADATA wsaData;
    int     result;

    if (!isWSAStartupDone)
    {
        auto lock = SRWExclusiveLock(m_srwLock);

        if (!isWSAStartupDone)
        {
            if ((result = WSAStartup(MAKEWORD(2, 2), &wsaData)) != 0)
            {
                RETURN_HR(HRESULT_FROM_WIN32(result));
            }
            isWSAStartupDone = true;
        }
    }

    m_rapidFailTickStart = GetTickCount64();

    if (m_hNULHandle == nullptr)
    {
        SECURITY_ATTRIBUTES saAttr;
        saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
        saAttr.bInheritHandle = TRUE;
        saAttr.lpSecurityDescriptor = nullptr;

        m_hNULHandle = CreateFileW(L"NUL",
                                   FILE_WRITE_DATA,
                                   FILE_SHARE_READ,
                                   &saAttr,
                                   CREATE_ALWAYS,
                                   FILE_ATTRIBUTE_NORMAL,
                                   nullptr);

        RETURN_LAST_ERROR_IF(m_hNULHandle == INVALID_HANDLE_VALUE);
    }

    return S_OK;
}

void PROCESS_MANAGER::ReferenceProcessManager() const
{
    ++m_cRefs;
}

void PROCESS_MANAGER::DereferenceProcessManager() const
{
    if (--m_cRefs == 0)
    {
        delete this;
    }
}

HRESULT PROCESS_MANAGER::GetProcess(
    _In_    REQUESTHANDLER_CONFIG      *pConfig,
    _In_    BOOL                        fWebsocketSupported,
    _Out_   SERVER_PROCESS            **ppServerProcess
)
{
    std::unique_ptr<SERVER_PROCESS>  pSelectedServerProcess;
    int processIndex;

    if (isStopping)
    {
        RETURN_IF_FAILED(E_APPLICATION_EXITING);
    }

    if (!serverProcessListReady)
    {
        auto lock = SRWExclusiveLock(m_srwLock);

        if (!serverProcessListReady)
        {
            m_dwProcessesPerApplication = pConfig->QueryProcessesPerApplication();
            m_ppServerProcessList = new SERVER_PROCESS*[m_dwProcessesPerApplication];

            for (DWORD i = 0; i < m_dwProcessesPerApplication; ++i)
            {
                m_ppServerProcessList[i] = nullptr;
            }
        }

        serverProcessListReady = true;
    }

    {
        auto lock = SRWSharedLock(m_srwLock);

        //
        // round robin through to the next available process.
        //
        processIndex = m_dwRouteToProcessIndex.fetch_add(1) % m_dwProcessesPerApplication;

        if (m_ppServerProcessList[processIndex] != nullptr &&
            m_ppServerProcessList[processIndex]->IsReady())
        {
            *ppServerProcess = m_ppServerProcessList[processIndex];
            return S_OK;
        }
    }

    // should make the lock per process so that we can start processes simultaneously ?
    if (m_ppServerProcessList[processIndex] == nullptr ||
        !m_ppServerProcessList[processIndex]->IsReady())
    {
        auto lock = SRWExclusiveLock(m_srwLock);

        if (m_ppServerProcessList[processIndex] != nullptr)
        {
            if (!m_ppServerProcessList[dwProcessIndex]->IsReady())
            {
                //
                // terminate existing process that is not ready
                // before creating new one.
                //
                ShutdownProcessNoLock( m_ppServerProcessList[dwProcessIndex] );
            }
            else
            {
                // server is already up and ready to serve requests.
                //m_ppServerProcessList[dwProcessIndex]->ReferenceServerProcess();
                *ppServerProcess = m_ppServerProcessList[dwProcessIndex];
                return S_OK;
            }
        }

        if (RapidFailsPerMinuteExceeded(pConfig->QueryRapidFailsPerMinute()))
        {
            //
            // rapid fails per minute exceeded, do not create new process.
            //
            EventLog::Info(
                ASPNETCORE_EVENT_RAPID_FAIL_COUNT_EXCEEDED,
                ASPNETCORE_EVENT_RAPID_FAIL_COUNT_EXCEEDED_MSG,
                pConfig->QueryRapidFailsPerMinute());

            RETURN_HR(HRESULT_FROM_WIN32(ERROR_SERVER_DISABLED));
        }

        if (m_ppServerProcessList[dwProcessIndex] == nullptr)
        {
            pSelectedServerProcess = std::make_unique<SERVER_PROCESS>();
            RETURN_IF_FAILED(pSelectedServerProcess->Initialize(
                    this,                                   //ProcessManager
                    pConfig->QueryProcessPath(),            //
                    pConfig->QueryArguments(),              //
                    pConfig->QueryStartupTimeLimitInMS(),
                    pConfig->QueryShutdownTimeLimitInMS(),
                    pConfig->QueryWindowsAuthEnabled(),
                    pConfig->QueryBasicAuthEnabled(),
                    pConfig->QueryAnonymousAuthEnabled(),
                    pConfig->QueryEnvironmentVariables(),
                    pConfig->QueryStdoutLogEnabled(),
                    pConfig->QueryEnableOutOfProcessConsoleRedirection(),
                    fWebsocketSupported,
                    pConfig->QueryStdoutLogFile(),
                    pConfig->QueryApplicationPhysicalPath(),   // physical path
                    pConfig->QueryApplicationPath(),           // app path
                    pConfig->QueryApplicationVirtualPath(),    // App relative virtual path,
                    pConfig->QueryBindings()
            ));
            RETURN_IF_FAILED(pSelectedServerProcess->StartProcess());
        }

        if (!pSelectedServerProcess->IsReady())
        {
            RETURN_HR(HRESULT_FROM_WIN32(ERROR_CREATE_FAILED));
        }

        m_ppServerProcessList[dwProcessIndex] = pSelectedServerProcess.release();
    }

    *ppServerProcess = m_ppServerProcessList[dwProcessIndex];
    return S_OK;
}

void PROCESS_MANAGER::SendShutdownSignal()
{
    AcquireSRWLockExclusive(&m_srwLock);

    for (DWORD i = 0; i < m_dwProcessesPerApplication; ++i)
    {
        if (m_ppServerProcessList != nullptr &&
            m_ppServerProcessList[i] != nullptr)
        {
            m_ppServerProcessList[i]->SendSignal();
            m_ppServerProcessList[i]->DereferenceServerProcess();
            m_ppServerProcessList[i] = nullptr;
        }
    }

    ReleaseSRWLockExclusive(&m_srwLock);
}

void PROCESS_MANAGER::ShutdownProcess(SERVER_PROCESS* pServerProcess)
{
    AcquireSRWLockExclusive(&m_srwLock);
    ShutdownProcessNoLock(pServerProcess);
    ReleaseSRWLockExclusive(&m_srwLock);
}

void PROCESS_MANAGER::ShutdownAllProcesses()
{
    AcquireSRWLockExclusive(&m_srwLock);
    ShutdownAllProcessesNoLock();
    ReleaseSRWLockExclusive(&m_srwLock);
}

void PROCESS_MANAGER::Shutdown()
{
    if (!isStopping.exchange(true))
    {
        ShutdownAllProcesses();
    }
}

void PROCESS_MANAGER::IncrementRapidFailCount()
{
    m_cRapidFailCount++;
}

static constexpr auto ONE_MINUTE_IN_MILLISECONDS = 60000;
bool PROCESS_MANAGER::RapidFailsPerMinuteExceeded(LONG dwRapidFailsPerMinute)
{
    uint64_t currentTickCount = GetTickCount64();

    if ((currentTickCount - m_rapidFailTickStart) >= ONE_MINUTE_IN_MILLISECONDS)
    {
        // reset counters every minute
        m_cRapidFailCount = 0;
        m_rapidFailTickStart = currentTickCount;
    }

    return m_cRapidFailCount > dwRapidFailsPerMinute;
}

void PROCESS_MANAGER::ShutdownProcessNoLock(SERVER_PROCESS* pServerProcess)
{
    for (DWORD i = 0; i < m_dwProcessesPerApplication; ++i)
    {
        if (m_ppServerProcessList != nullptr &&
            m_ppServerProcessList[i] != nullptr &&
            m_ppServerProcessList[i]->GetPort() == pServerProcess->GetPort())
        {
            // shutdown pServerProcess if not already shutdown.
            m_ppServerProcessList[i]->StopProcess();
            m_ppServerProcessList[i]->DereferenceServerProcess();
            m_ppServerProcessList[i] = nullptr;
        }
    }
}

void PROCESS_MANAGER::ShutdownAllProcessesNoLock()
{
    for (DWORD i = 0; i < m_dwProcessesPerApplication; ++i)
    {
        if (m_ppServerProcessList != nullptr &&
            m_ppServerProcessList[i] != nullptr)
        {
            // shutdown pServerProcess if not already shutdown.
            m_ppServerProcessList[i]->SendSignal();
            m_ppServerProcessList[i]->DereferenceServerProcess();
            m_ppServerProcessList[i] = nullptr;
        }
    }
}
