using IntentOverHttps.Core.Abstractions;

namespace IntentOverHttps.DemoWeb.Services;

public interface IDemoIntentKeyMaterialStore : IKeyResolver
{
    SigningKeyMaterial GetCurrentSigningKey();

    IReadOnlyList<PublishedIntentKey> GetPublishedKeys();
}

