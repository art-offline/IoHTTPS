using System.Security.Cryptography;
using System.Text;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;

namespace IntentOverHttps.DemoWeb.Services;

public sealed class EcdsaIntentSigner : IIntentSigner
{
    private readonly IDemoIntentKeyMaterialStore _keyMaterialStore;
    private readonly IntentHeaderSerializer _serializer;

    public EcdsaIntentSigner(IDemoIntentKeyMaterialStore keyMaterialStore, IntentHeaderSerializer serializer)
    {
        _keyMaterialStore = keyMaterialStore;
        _serializer = serializer;
    }

    public ValueTask<byte[]> SignAsync(IntentDescriptor intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = Encoding.UTF8.GetBytes(_serializer.Serialize(intent));
        var signature = _keyMaterialStore
            .GetCurrentSigningKey()
            .SigningKey
            .SignData(payload, HashAlgorithmName.SHA256);

        return ValueTask.FromResult(signature);
    }
}

