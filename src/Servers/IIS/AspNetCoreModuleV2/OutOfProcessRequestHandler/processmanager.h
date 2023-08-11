// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#pragma once

class SERVER_PROCESS;

class PROCESS_MANAGER
{
public:
    virtual ~PROCESS_MANAGER();

    void ReferenceProcessManager() const;
    void DereferenceProcessManager() const;

    HRESULT GetProcess(
        _In_    REQUESTHANDLER_CONFIG      *pConfig,
        _In_    BOOL                        fWebsocketEnabled,
        _Out_   SERVER_PROCESS            **ppServerProcess
    );

    HANDLE QueryNULHandle() const
    {
        return m_hNULHandle;
    }

    HRESULT Initialize();

    void SendShutdownSignal();
    void ShutdownProcess(SERVER_PROCESS* pServerProcess);
    void ShutdownAllProcesses();
    void Shutdown();

    void IncrementRapidFailCount();

    PROCESS_MANAGER();

private:
    bool RapidFailsPerMinuteExceeded(LONG dwRapidFailsPerMinute);
    void ShutdownProcessNoLock(SERVER_PROCESS* pServerProcess);
    void ShutdownAllProcessesNoLock();


    std::atomic_long     m_cRapidFailCount{ 0 };
    uint64_t             m_rapidFailTickStart;
    DWORD                m_dwProcessesPerApplication{ 1 };
    std::atomic<int>     m_dwRouteToProcessIndex{ 0 };

    SRWLOCK              m_srwLock;
    SERVER_PROCESS     **m_ppServerProcessList;

    //
    // m_hNULHandle is used to redirect stdout/stderr to NUL.
    // If Createprocess is called to launch a batch file for example,
    // it tries to write to the console buffer by default. It fails to 
    // start if the console buffer is owned by the parent process i.e 
    // in our case w3wp.exe. So we have to redirect the stdout/stderr
    // of the child process to NUL or to a file (anything other than
    // the console buffer of the parent process).
    //

    HANDLE m_hNULHandle{ nullptr };
    mutable std::atomic_long m_cRefs{ 1 };
    static std::atomic_bool isWSAStartupDone;
    std::atomic_bool serverProcessListReady{ false };
    std::atomic_bool isStopping{ false };
};
