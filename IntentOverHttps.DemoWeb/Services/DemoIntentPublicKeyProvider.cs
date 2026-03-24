using IntentOverHttps.AspNetCore.KeyDiscovery;

namespace IntentOverHttps.DemoWeb.Services;

public sealed class DemoIntentPublicKeyProvider : IIntentPublicKeyProvider
{
    private readonly IDemoIntentKeyMaterialStore _store;

    public DemoIntentPublicKeyProvider(IDemoIntentKeyMaterialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public ValueTask<IReadOnlyList<IntentPublicKey>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keys = _store.GetPublishedKeys()
            .Select(static key => new IntentPublicKey(key.Kid, key.Kty, key.Crv, key.Use, key.Alg, key.X, key.Y))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<IntentPublicKey>>(keys);
    }
}

