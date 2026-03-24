using System.Security.Cryptography;

namespace IntentOverHttps.DemoWeb.Services;

public sealed class SigningKeyMaterial
{
    public SigningKeyMaterial(string issuer, string keyId, string algorithm, ECDsa signingKey, byte[] publicKeySubjectPublicKeyInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(publicKeySubjectPublicKeyInfo);

        Issuer = issuer;
        KeyId = keyId;
        Algorithm = algorithm;
        SigningKey = signingKey;
        PublicKeySubjectPublicKeyInfo = publicKeySubjectPublicKeyInfo.ToArray();
    }

    public string Issuer { get; }

    public string KeyId { get; }

    public string Algorithm { get; }

    public ECDsa SigningKey { get; }

    public byte[] PublicKeySubjectPublicKeyInfo { get; }
}

