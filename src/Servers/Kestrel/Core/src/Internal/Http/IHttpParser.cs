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

    private HttpParseResult(bool isComplete, RequestRejectionReason errorReason)
    {
        _errorReason = errorReason;
        _flags = (byte)((isComplete ? 1 : 0) | (errorReason != default ? 2 : 0));
    }

    /// <summary>True if parsing completed or needs more data (no error).</summary>
    public bool IsSuccess => (_flags & 2) == 0;
    
    /// <summary>True if parsing completed successfully.</summary>
    public bool IsComplete => (_flags & 1) != 0 && (_flags & 2) == 0;
    
    /// <summary>True if more data is needed.</summary>
    public bool NeedsMoreData => (_flags & 3) == 0;
    
    /// <summary>True if a parse error occurred.</summary>
    public bool HasError => (_flags & 2) != 0;
    
    /// <summary>The reason for rejection, if HasError is true.</summary>
    public RequestRejectionReason ErrorReason => _errorReason;

    /// <summary>Parsing needs more data.</summary>
    public static HttpParseResult Incomplete => new(false, default);
    
    /// <summary>Parsing completed successfully.</summary>
    public static HttpParseResult Complete => new(true, default);
    
    /// <summary>Parsing failed with the specified error.</summary>
    public static HttpParseResult Error(RequestRejectionReason reason) => new(false, reason);
}

internal interface IHttpParser<TRequestHandler> where TRequestHandler : IHttpHeadersHandler, IHttpRequestLineHandler
{
    bool ParseRequestLine(TRequestHandler handler, ref SequenceReader<byte> reader);

    bool ParseHeaders(TRequestHandler handler, ref SequenceReader<byte> reader);
}
