using IntentOverHttps.AspNetCore.Signing;
using IntentOverHttps.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentOverHttps.AspNetCore;

/// <summary>
/// Extension methods for registering IoHTTPS services in the DI container.
/// </summary>
public static class IntentServiceCollectionExtensions
{
    /// <summary>
    /// Registers IoHTTPS core infrastructure services.
    /// </summary>
    /// <remarks>
    /// The following services must be registered separately by the application:
    /// <list type="bullet">
    ///   <item><see cref="Core.Abstractions.IIntentSigner"/> — signing implementation.</item>
    ///   <item><see cref="Signing.IIntentKeyMetadataProvider"/> — active key id and algorithm.</item>
    ///   <item><see cref="KeyDiscovery.IIntentPublicKeyProvider"/> — required only when <c>MapIntentKeyDiscovery()</c> is used.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddIntentOverHttps(
        this IServiceCollection services,
        Action<IntentProtocolOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<IntentProtocolOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IntentHeaderSerializer>();
        services.TryAddSingleton<IntentHeaderParser>();
        services.TryAddSingleton<IIntentResponseSigner, IntentResponseSigner>();

        return services;
    }
}

