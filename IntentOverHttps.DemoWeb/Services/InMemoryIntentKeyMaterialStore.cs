using System.Security.Cryptography;
using IntentOverHttps.DemoWeb.Helpers;
using IntentOverHttps.DemoWeb.Options;
using IntentOverHttps.DemoWeb.Protocol;
using Microsoft.Extensions.Options;

namespace IntentOverHttps.DemoWeb.Services;

public sealed class InMemoryIntentKeyMaterialStore : IDemoIntentKeyMaterialStore, IDisposable
{
    private readonly SigningKeyMaterial _currentSigningKey;
    private readonly IReadOnlyList<PublishedIntentKey> _publishedKeys;

    public InMemoryIntentKeyMaterialStore(IOptions<DemoIntentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyInfo = signingKey.ExportSubjectPublicKeyInfo();
        var publicParameters = signingKey.ExportParameters(includePrivateParameters: false);
        var x = publicParameters.Q.X ?? throw new InvalidOperationException("Missing X coordinate for the generated ECDSA public key.");
        var y = publicParameters.Q.Y ?? throw new InvalidOperationException("Missing Y coordinate for the generated ECDSA public key.");

        _currentSigningKey = new SigningKeyMaterial(
            settings.Issuer,
            settings.KeyId,
            IntentProtocolConstants.Algorithm,
            signingKey,
            publicKeyInfo);

        _publishedKeys =
        [
            new PublishedIntentKey(
                settings.KeyId,
                Kty: "EC",
                Crv: "P-256",
                Use: "sig",
                Alg: IntentProtocolConstants.Algorithm,
                X: Base64Url.Encode(x),
                Y: Base64Url.Encode(y))
        ];
    }

    public SigningKeyMaterial GetCurrentSigningKey() => _currentSigningKey;

    public IReadOnlyList<PublishedIntentKey> GetPublishedKeys() => _publishedKeys;

    public ValueTask<byte[]?> ResolveKeyAsync(string issuer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(issuer, _currentSigningKey.Issuer, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<byte[]?>(null);
        }

        return ValueTask.FromResult<byte[]?>(_currentSigningKey.PublicKeySubjectPublicKeyInfo.ToArray());
    }

    public void Dispose()
    {
        _currentSigningKey.SigningKey.Dispose();
    }
}

