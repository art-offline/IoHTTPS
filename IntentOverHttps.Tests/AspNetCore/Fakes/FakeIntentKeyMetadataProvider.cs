using IntentOverHttps.AspNetCore.Signing;

namespace IntentOverHttps.Tests.AspNetCore.Fakes;

/// <summary>
/// Test double for <see cref="IIntentKeyMetadataProvider"/> that returns fixed metadata.
/// </summary>
internal sealed class FakeIntentKeyMetadataProvider : IIntentKeyMetadataProvider
{
    private readonly IntentKeyMetadata _metadata;

    public FakeIntentKeyMetadataProvider(string keyId = "test-key-1", string algorithm = "ES256")
    {
        _metadata = new IntentKeyMetadata(keyId, algorithm);
    }

    public ValueTask<IntentKeyMetadata> GetCurrentKeyMetadataAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_metadata);
}

