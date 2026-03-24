using System.Security.Cryptography;
using System.Text;

namespace IntentOverHttps.Cli.Crypto;

internal static class PemCodec
{
    public static ECDsa ImportPrivateKey(string pemOrBase64)
    {
        var material = NormalizeInput(pemOrBase64);
        var ecdsa = ECDsa.Create();

        try
        {
            if (LooksLikePem(material))
            {
                ecdsa.ImportFromPem(material);
                return ecdsa;
            }

            var pkcs8 = Convert.FromBase64String(material);
            ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw new ArgumentException("Private key must be a valid PKCS#8 PEM or Base64-encoded PKCS#8 value.");
        }
    }

    public static ECDsa ImportPublicKey(string pemOrBase64)
    {
        var material = NormalizeInput(pemOrBase64);
        var ecdsa = ECDsa.Create();

        try
        {
            if (LooksLikePem(material))
            {
                ecdsa.ImportFromPem(material);
                return ecdsa;
            }

            var spki = Convert.FromBase64String(material);
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw new ArgumentException("Public key must be a valid PEM or Base64-encoded SubjectPublicKeyInfo value.");
        }
    }

    public static string ExportPrivateKeyPem(ECDsa ecdsa)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    public static string ExportPublicKeyPem(ECDsa ecdsa)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        return ecdsa.ExportSubjectPublicKeyInfoPem();
    }

    public static byte[] ExportPublicKeySpki(ECDsa ecdsa)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        return ecdsa.ExportSubjectPublicKeyInfo();
    }

    public static (string X, string Y) ExportJwkCoordinates(ECDsa ecdsa)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        var parameters = ecdsa.ExportParameters(false);

        if (parameters.Q.X is null || parameters.Q.Y is null)
        {
            throw new InvalidOperationException("Unable to export EC public key coordinates.");
        }

        return (Base64Url.Encode(parameters.Q.X), Base64Url.Encode(parameters.Q.Y));
    }

    public static string CreateKeyId(ReadOnlySpan<byte> spki)
    {
        var hash = SHA256.HashData(spki);
        var prefix = Base64Url.Encode(hash);
        return $"dev-{prefix[..16]}";
    }

    private static string NormalizeInput(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static bool LooksLikePem(string value) => value.Contains("-----BEGIN", StringComparison.Ordinal);
}

