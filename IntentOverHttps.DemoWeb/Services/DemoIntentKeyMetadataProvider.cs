using IntentOverHttps.AspNetCore.Signing;

namespace IntentOverHttps.DemoWeb.Services;

public sealed class DemoIntentKeyMetadataProvider : IIntentKeyMetadataProvider
{
    private readonly IDemoIntentKeyMaterialStore _store;

    public DemoIntentKeyMetadataProvider(IDemoIntentKeyMaterialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public ValueTask<IntentKeyMetadata> GetCurrentKeyMetadataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signingKey = _store.GetCurrentSigningKey();
        return ValueTask.FromResult(new IntentKeyMetadata(signingKey.KeyId, signingKey.Algorithm));
    }
}

