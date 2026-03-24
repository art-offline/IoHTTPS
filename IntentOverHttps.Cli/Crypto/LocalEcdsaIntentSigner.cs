using System.Security.Cryptography;
using System.Text;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;

namespace IntentOverHttps.Cli.Crypto;

internal sealed class LocalEcdsaIntentSigner : IIntentSigner
{
    private readonly ECDsa _signingKey;
    private readonly IntentHeaderSerializer _serializer;

    public LocalEcdsaIntentSigner(ECDsa signingKey, IntentHeaderSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(serializer);

        _signingKey = signingKey;
        _serializer = serializer;
    }

    public ValueTask<byte[]> SignAsync(IntentDescriptor intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = Encoding.UTF8.GetBytes(_serializer.Serialize(intent));
        var signature = _signingKey.SignData(payload, HashAlgorithmName.SHA256);
        return ValueTask.FromResult(signature);
    }
}

