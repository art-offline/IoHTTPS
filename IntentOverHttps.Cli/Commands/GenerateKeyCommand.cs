using System.Security.Cryptography;
using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Crypto;
using IntentOverHttps.Cli.Parsing;

namespace IntentOverHttps.Cli.Commands;

internal sealed class GenerateKeyCommand : ICliCommand
{
    public string Name => "generate-key";

    public string Summary => "Generate a new ES256 signing key pair for development/testing.";

    public string HelpText => """
Usage:
  IntentOverHttps.Cli generate-key [options]

Options:
  --kid <value>     Optional key id to print. If omitted, a dev-friendly kid is derived from the public key.
  --help            Show command help.

Description:
  Generates a new ECDSA P-256 key pair suitable for ES256 signing.
  Prints the private key (PKCS#8 PEM), public key (SPKI PEM), a Base64 SPKI form,
  a suggested key id, and JWK x/y coordinates for docs and testing.
""";

    public Task<int> ExecuteAsync(CommandArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = PemCodec.ExportPublicKeySpki(ecdsa);
        var kid = arguments.GetOptional("kid") ?? PemCodec.CreateKeyId(spki);
        var privatePem = PemCodec.ExportPrivateKeyPem(ecdsa);
        var publicPem = PemCodec.ExportPublicKeyPem(ecdsa);
        var publicSpkiBase64 = Convert.ToBase64String(spki);
        var (x, y) = PemCodec.ExportJwkCoordinates(ecdsa);

        ConsolePrinter.WriteSection("Generated Signing Key");
        ConsolePrinter.WriteKeyValue("Algorithm", "ES256");
        ConsolePrinter.WriteKeyValue("Curve", "P-256");
        ConsolePrinter.WriteKeyValue("Key Id", kid);
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Private Key (PKCS#8 PEM)");
        Console.WriteLine(privatePem.Trim());
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Public Key (SPKI PEM)");
        Console.WriteLine(publicPem.Trim());
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Public Key (SPKI Base64)");
        Console.WriteLine(publicSpkiBase64);
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("JWK Coordinates");
        ConsolePrinter.WriteKeyValue("kty", "EC");
        ConsolePrinter.WriteKeyValue("crv", "P-256");
        ConsolePrinter.WriteKeyValue("use", "sig");
        ConsolePrinter.WriteKeyValue("alg", "ES256");
        ConsolePrinter.WriteKeyValue("kid", kid);
        ConsolePrinter.WriteKeyValue("x", x);
        ConsolePrinter.WriteKeyValue("y", y);

        return Task.FromResult(0);
    }
}

