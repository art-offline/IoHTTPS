namespace IntentOverHttps.AspNetCore;

/// <summary>
/// Configuration options for the IoHTTPS integration layer.
/// Register via <c>AddIntentOverHttps(opts => ...)</c>.
/// </summary>
public sealed class IntentProtocolOptions
{
    /// <summary>
    /// The issuer identifier published in the key-discovery document.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Protocol version written to the <c>Intent-Version</c> response header.
    /// Defaults to <c>"1"</c>.
    /// </summary>
    public string Version { get; set; } = "1";

    /// <summary>
    /// HTTP path for the key-discovery endpoint registered by
    /// <c>MapIntentKeyDiscovery()</c>.
    /// Defaults to <c>"/.well-known/intent-keys"</c>.
    /// </summary>
    public string WellKnownPath { get; set; } = "/.well-known/intent-keys";
}

