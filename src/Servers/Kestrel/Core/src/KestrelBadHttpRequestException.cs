// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Server.Kestrel.Core;

internal static class KestrelBadHttpRequestException
{
    // Cached exception instances for common rejection reasons (without detail strings)
    // These avoid allocation overhead for high-frequency bad request scenarios
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly BadHttpRequestException CachedInvalidRequestLine =
        new(CoreStrings.BadRequest_InvalidRequestLine, StatusCodes.Status400BadRequest, RequestRejectionReason.InvalidRequestLine);
    private static readonly BadHttpRequestException CachedMalformedRequestInvalidHeaders =
        new(CoreStrings.BadRequest_MalformedRequestInvalidHeaders, StatusCodes.Status400BadRequest, RequestRejectionReason.MalformedRequestInvalidHeaders);
    private static readonly BadHttpRequestException CachedInvalidRequestHeadersNoCRLF =
        new(CoreStrings.BadRequest_InvalidRequestHeadersNoCRLF, StatusCodes.Status400BadRequest, RequestRejectionReason.InvalidRequestHeadersNoCRLF);
    private static readonly BadHttpRequestException CachedInvalidCharactersInHeaderName =
        new(CoreStrings.BadRequest_InvalidCharactersInHeaderName, StatusCodes.Status400BadRequest, RequestRejectionReason.InvalidCharactersInHeaderName);
    private static readonly BadHttpRequestException CachedRequestHeadersTimeout =
        new(CoreStrings.BadRequest_RequestHeadersTimeout, StatusCodes.Status408RequestTimeout, RequestRejectionReason.RequestHeadersTimeout);
    private static readonly BadHttpRequestException CachedRequestLineTooLong =
        new(CoreStrings.BadRequest_RequestLineTooLong, StatusCodes.Status414UriTooLong, RequestRejectionReason.RequestLineTooLong);
    private static readonly BadHttpRequestException CachedHeadersExceedMaxTotalSize =
        new(CoreStrings.BadRequest_HeadersExceedMaxTotalSize, StatusCodes.Status431RequestHeaderFieldsTooLarge, RequestRejectionReason.HeadersExceedMaxTotalSize);
    private static readonly BadHttpRequestException CachedTooManyHeaders =
        new(CoreStrings.BadRequest_TooManyHeaders, StatusCodes.Status431RequestHeaderFieldsTooLarge, RequestRejectionReason.TooManyHeaders);
    private static readonly BadHttpRequestException CachedGenericBadRequest =
        new(CoreStrings.BadRequest, StatusCodes.Status400BadRequest, RequestRejectionReason.InvalidRequestHeader);
#pragma warning restore CS0618 // Type or member is obsolete

    [StackTraceHidden]
    internal static void Throw(RequestRejectionReason reason)
    {
        throw GetException(reason);
    }

    [StackTraceHidden]
    internal static void Throw(RequestRejectionReason reason, HttpMethod method)
        => throw GetException(reason, method.ToString().ToUpperInvariant());

    [MethodImpl(MethodImplOptions.NoInlining)]
#pragma warning disable CS0618 // Type or member is obsolete
    internal static BadHttpRequestException GetException(RequestRejectionReason reason)
    {
        // Return cached instances for common rejection reasons to avoid allocation
        return reason switch
        {
            RequestRejectionReason.InvalidRequestLine => CachedInvalidRequestLine,
            RequestRejectionReason.MalformedRequestInvalidHeaders => CachedMalformedRequestInvalidHeaders,
            RequestRejectionReason.InvalidRequestHeadersNoCRLF => CachedInvalidRequestHeadersNoCRLF,
            RequestRejectionReason.InvalidCharactersInHeaderName => CachedInvalidCharactersInHeaderName,
            RequestRejectionReason.RequestHeadersTimeout => CachedRequestHeadersTimeout,
            RequestRejectionReason.RequestLineTooLong => CachedRequestLineTooLong,
            RequestRejectionReason.HeadersExceedMaxTotalSize => CachedHeadersExceedMaxTotalSize,
            RequestRejectionReason.TooManyHeaders => CachedTooManyHeaders,
            RequestRejectionReason.InvalidRequestHeader => CachedGenericBadRequest,
            // Less common reasons still allocate (acceptable trade-off)
            _ => CreateException(reason)
        };
    }

    /// <summary>
    /// Creates a new exception for less common rejection reasons (not cached).
    /// </summary>
    private static BadHttpRequestException CreateException(RequestRejectionReason reason)
    {
        return reason switch
        {
            RequestRejectionReason.MultipleContentLengths =>
                new BadHttpRequestException(CoreStrings.BadRequest_MultipleContentLengths, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.UnexpectedEndOfRequestContent =>
                new BadHttpRequestException(CoreStrings.BadRequest_UnexpectedEndOfRequestContent, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.BadChunkSuffix =>
                new BadHttpRequestException(CoreStrings.BadRequest_BadChunkSuffix, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.BadChunkSizeData =>
                new BadHttpRequestException(CoreStrings.BadRequest_BadChunkSizeData, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.BadChunkExtension =>
                new BadHttpRequestException(CoreStrings.BadRequest_BadChunkExtension, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.ChunkedRequestIncomplete =>
                new BadHttpRequestException(CoreStrings.BadRequest_ChunkedRequestIncomplete, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.RequestBodyTimeout =>
                new BadHttpRequestException(CoreStrings.BadRequest_RequestBodyTimeout, StatusCodes.Status408RequestTimeout, reason),
            RequestRejectionReason.OptionsMethodRequired =>
                new BadHttpRequestException(CoreStrings.BadRequest_MethodNotAllowed, StatusCodes.Status405MethodNotAllowed, reason, HttpMethod.Options),
            RequestRejectionReason.ConnectMethodRequired =>
                new BadHttpRequestException(CoreStrings.BadRequest_MethodNotAllowed, StatusCodes.Status405MethodNotAllowed, reason, HttpMethod.Connect),
            RequestRejectionReason.MissingHostHeader =>
                new BadHttpRequestException(CoreStrings.BadRequest_MissingHostHeader, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.MultipleHostHeaders =>
                new BadHttpRequestException(CoreStrings.BadRequest_MultipleHostHeaders, StatusCodes.Status400BadRequest, reason),
            RequestRejectionReason.InvalidHostHeader =>
                new BadHttpRequestException(CoreStrings.BadRequest_InvalidHostHeader, StatusCodes.Status400BadRequest, reason),
            _ =>
                new BadHttpRequestException(CoreStrings.BadRequest, StatusCodes.Status400BadRequest, reason)
        };
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [StackTraceHidden]
    internal static void Throw(RequestRejectionReason reason, string detail)
    {
        throw GetException(reason, detail);
    }

    [StackTraceHidden]
    internal static void Throw(RequestRejectionReason reason, StringValues detail)
    {
        throw GetException(reason, detail.ToString());
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static BadHttpRequestException GetException(RequestRejectionReason reason, string detail)
    {
        BadHttpRequestException ex;
        switch (reason)
        {
            case RequestRejectionReason.TlsOverHttpError:
                ex = new BadHttpRequestException(CoreStrings.HttpParserTlsOverHttpError, StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.InvalidRequestLine:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_InvalidRequestLine_Detail(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.InvalidRequestTarget:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_InvalidRequestTarget_Detail(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.InvalidRequestHeader:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_InvalidRequestHeader_Detail(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.InvalidContentLength:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_InvalidContentLength_Detail(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.UnrecognizedHTTPVersion:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_UnrecognizedHTTPVersion(detail), StatusCodes.Status505HttpVersionNotsupported, reason);
                break;
            case RequestRejectionReason.FinalTransferCodingNotChunked:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_FinalTransferCodingNotChunked(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.LengthRequiredHttp10:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_LengthRequiredHttp10(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.InvalidHostHeader:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_InvalidHostHeader_Detail(detail), StatusCodes.Status400BadRequest, reason);
                break;
            case RequestRejectionReason.RequestBodyTooLarge:
                ex = new BadHttpRequestException(CoreStrings.FormatBadRequest_RequestBodyTooLarge(detail), StatusCodes.Status413PayloadTooLarge, reason);
                break;
            default:
                ex = new BadHttpRequestException(CoreStrings.BadRequest, StatusCodes.Status400BadRequest, reason);
                break;
        }
        return ex;
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
