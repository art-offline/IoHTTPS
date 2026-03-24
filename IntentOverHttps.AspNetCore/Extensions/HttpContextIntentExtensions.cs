using System.Text.Json;
using IntentOverHttps.AspNetCore.Signing;
using IntentOverHttps.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntentOverHttps.AspNetCore;

/// <summary>
/// Extension methods on <see cref="HttpContext"/> for writing intent-signed JSON responses.
/// </summary>
public static class HttpContextIntentExtensions
{
    /// <summary>
    /// Signs <paramref name="descriptor"/>, writes all five IoHTTPS protocol headers,
    /// then serializes <paramref name="body"/> as the JSON response body.
    /// </summary>
    /// <typeparam name="T">The type of the response body object.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="descriptor">The intent descriptor to sign and attach.</param>
    /// <param name="body">The JSON response body.</param>
    /// <param name="jsonOptions">Optional JSON serializer options. Uses the registered
    /// <c>JsonSerializerOptions</c> from DI when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task WriteIntentSignedJsonAsync<T>(
        this HttpContext context,
        IntentDescriptor descriptor,
        T body,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var signer = context.RequestServices.GetRequiredService<IIntentResponseSigner>();
        await signer.WriteHeadersAsync(context.Response, descriptor, cancellationToken);
        await context.Response.WriteAsJsonAsync(body, jsonOptions, cancellationToken);
    }
}

