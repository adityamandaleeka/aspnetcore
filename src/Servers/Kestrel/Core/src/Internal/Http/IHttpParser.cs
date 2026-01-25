// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

/// <summary>
/// Result of non-throwing HTTP parsing operations.
/// </summary>
internal readonly struct HttpParseResult
{
    private readonly RequestRejectionReason _errorReason;
    private readonly byte _flags; // bit 0 = complete, bit 1 = hasError
    private readonly int _errorOffset;
    private readonly int _errorLength;

    private HttpParseResult(bool isComplete, bool hasError, RequestRejectionReason errorReason, int errorOffset = 0, int errorLength = 0)
    {
        _errorReason = errorReason;
        _flags = (byte)((isComplete ? 1 : 0) | (hasError ? 2 : 0));
        _errorOffset = errorOffset;
        _errorLength = errorLength;
    }

    /// <summary>True if parsing completed successfully.</summary>
    public bool IsComplete => (_flags & 1) != 0 && (_flags & 2) == 0;

    /// <summary>True if a parse error occurred.</summary>
    public bool HasError => (_flags & 2) != 0;

    /// <summary>The reason for rejection, if HasError is true.</summary>
    public RequestRejectionReason ErrorReason => _errorReason;

    /// <summary>Offset into the buffer where the error was detected.</summary>
    public int ErrorOffset => _errorOffset;

    /// <summary>Length of the problematic data, for error reporting.</summary>
    public int ErrorLength => _errorLength;

    /// <summary>Parsing needs more data.</summary>
    public static HttpParseResult Incomplete => new(false, false, default);

    /// <summary>Parsing completed successfully.</summary>
    public static HttpParseResult Complete => new(true, false, default);

    /// <summary>Parsing failed with the specified error.</summary>
    public static HttpParseResult Error(RequestRejectionReason reason) => new(false, true, reason);

    /// <summary>Parsing failed with the specified error and location info for detailed error messages.</summary>
    public static HttpParseResult Error(RequestRejectionReason reason, int offset, int length) => new(false, true, reason, offset, length);
}

internal interface IHttpParser<TRequestHandler> where TRequestHandler : IHttpHeadersHandler, IHttpRequestLineHandler
{
    bool ParseRequestLine(TRequestHandler handler, ref SequenceReader<byte> reader);

    bool ParseHeaders(TRequestHandler handler, ref SequenceReader<byte> reader);
}
