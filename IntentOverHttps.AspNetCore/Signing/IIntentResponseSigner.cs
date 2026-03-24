using IntentOverHttps.Core.Models;
using Microsoft.AspNetCore.Http;

namespace IntentOverHttps.AspNetCore.Signing;

/// <summary>
/// Encapsulates writing all IoHTTPS protocol headers to an HTTP response.
/// Register the default implementation via <c>AddIntentOverHttps()</c>.
/// </summary>
public interface IIntentResponseSigner
{
    /// <summary>
    /// Signs <paramref name="descriptor"/> and writes the five protocol headers
    /// (<c>Intent</c>, <c>Intent-Signature</c>, <c>Intent-Key-Id</c>,
    /// <c>Intent-Alg</c>, <c>Intent-Version</c>) onto <paramref name="response"/>.
    /// </summary>
    /// <remarks>
    /// Call this before writing the response body.
    /// Headers cannot be modified after the response has started.
    /// </remarks>
    ValueTask WriteHeadersAsync(
        HttpResponse response,
        IntentDescriptor descriptor,
        CancellationToken cancellationToken = default);
}

