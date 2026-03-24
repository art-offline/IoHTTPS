namespace IntentOverHttps.AspNetCore.KeyDiscovery;

/// <summary>
/// Supplies the list of public keys to be advertised at
/// <c>/.well-known/intent-keys</c>.
/// Required only when <c>MapIntentKeyDiscovery()</c> is used.
/// </summary>
public interface IIntentPublicKeyProvider
{
    /// <summary>Returns all currently active public keys for the issuer.</summary>
    ValueTask<IReadOnlyList<IntentPublicKey>> GetKeysAsync(CancellationToken cancellationToken = default);
}

