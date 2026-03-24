using IntentOverHttps.AspNetCore.Internal;
using IntentOverHttps.AspNetCore.KeyDiscovery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntentOverHttps.AspNetCore;

/// <summary>
/// Extension methods for mapping IoHTTPS protocol endpoints.
/// </summary>
public static class IntentEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the key-discovery endpoint at the path configured in
    /// <see cref="IntentProtocolOptions.WellKnownPath"/> (default <c>/.well-known/intent-keys</c>).
    /// Requires <see cref="KeyDiscovery.IIntentPublicKeyProvider"/> to be registered in DI.
    /// </summary>
    public static IEndpointRouteBuilder MapIntentKeyDiscovery(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<IntentProtocolOptions>>()
            .Value;

        endpoints
            .MapGet(options.WellKnownPath, HandleAsync)
            .WithName("GetIntentKeys")
            .WithTags("Intent Protocol")
            .Produces<IntentKeyDiscoveryResponse>();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        IIntentPublicKeyProvider keyProvider,
        IOptions<IntentProtocolOptions> options,
        CancellationToken cancellationToken)
    {
        var keys = await keyProvider.GetKeysAsync(cancellationToken);
        var opts = options.Value;

        httpContext.Response.Headers.CacheControl = "no-store";

        var response = new IntentKeyDiscoveryResponse(
            Issuer: opts.Issuer,
            Version: opts.Version,
            Keys: keys
                .Select(static k => new IntentPublicKeyJson(k.Kid, k.Kty, k.Crv, k.Use, k.Alg, k.X, k.Y))
                .ToArray());

        return TypedResults.Ok(response);
    }
}

