using IntentOverHttps.AspNetCore.KeyDiscovery;

namespace IntentOverHttps.Tests.AspNetCore.Fakes;

/// <summary>
/// Test double for <see cref="IIntentPublicKeyProvider"/> that returns a fixed key list.
/// </summary>
internal sealed class FakeIntentPublicKeyProvider : IIntentPublicKeyProvider
{
    private readonly IReadOnlyList<IntentPublicKey> _keys;

    public FakeIntentPublicKeyProvider(IReadOnlyList<IntentPublicKey>? keys = null)
    {
        _keys = keys ?? [new IntentPublicKey("test-key-1", "EC", "P-256", "sig", "ES256", "AAAA", "BBBB")];
    }

    public ValueTask<IReadOnlyList<IntentPublicKey>> GetKeysAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_keys);
}

