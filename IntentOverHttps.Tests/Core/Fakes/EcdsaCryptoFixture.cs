using System.Security.Cryptography;

namespace IntentOverHttps.Tests.Core.Fakes;

/// <summary>
/// xUnit class fixture that owns a single ECDSA P-256 key pair for the lifetime
/// of a test class. Tests receive this fixture via <c>IClassFixture&lt;EcdsaCryptoFixture&gt;</c>.
/// </summary>
public sealed class EcdsaCryptoFixture : IDisposable
{
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>
    /// The SubjectPublicKeyInfo (SPKI) bytes of the generated public key,
    /// suitable for passing to <see cref="ECDsa.ImportSubjectPublicKeyInfo"/>.
    /// </summary>
    public byte[] PublicKeyBytes { get; }

    public EcdsaCryptoFixture()
    {
        PublicKeyBytes = _signingKey.ExportSubjectPublicKeyInfo();
    }

    /// <summary>Signs <paramref name="payload"/> with ECDSA-SHA256.</summary>
    public byte[] Sign(byte[] payload)
        => _signingKey.SignData(payload, HashAlgorithmName.SHA256);

    public void Dispose() => _signingKey.Dispose();
}

