using System.Security.Cryptography;
using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Crypto;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;

namespace IntentOverHttps.Cli.Commands;

internal sealed class ShowExampleCommand : ICliCommand
{
    private readonly IntentHeaderSerializer _serializer = new();

    public string Name => "show-example";

    public string Summary => "Print a complete example IoHTTPS header set for docs/testing.";

    public string HelpText => """
Usage:
  IntentOverHttps.Cli show-example

Description:
  Generates a fresh, valid IoHTTPS example at runtime using a temporary ES256 key.
  Prints the five protocol headers plus the matching public key so the output can
  immediately be reused with verify-intent.
""";

    public async Task<int> ExecuteAsync(Parsing.CommandArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var descriptor = new IntentDescriptor(
            action: "pay",
            issuer: "example-merchant",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "merchant-123",
            amount: 12.34m,
            currency: "EUR",
            issuedAt: now,
            expiresAt: now.AddMinutes(5),
            nonce: Guid.NewGuid().ToString("N"));

        var signer = new LocalEcdsaIntentSigner(ecdsa, _serializer);
        var canonical = _serializer.Serialize(descriptor);
        var signature = await signer.SignAsync(descriptor, cancellationToken);
        var keyId = PemCodec.CreateKeyId(PemCodec.ExportPublicKeySpki(ecdsa));

        ConsolePrinter.WriteWarning("This example is generated at runtime, so the nonce, timestamps and signature will vary.");
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("IoHTTPS Headers");
        Console.WriteLine($"{IntentHeaderNames.Intent}: {canonical}");
        Console.WriteLine($"{IntentHeaderNames.Signature}: {Base64Url.Encode(signature)}");
        Console.WriteLine($"{IntentHeaderNames.KeyId}: {keyId}");
        Console.WriteLine($"{IntentHeaderNames.Algorithm}: ES256");
        Console.WriteLine($"{IntentHeaderNames.Version}: 1");
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Matching Public Key (SPKI PEM)");
        Console.WriteLine(PemCodec.ExportPublicKeyPem(ecdsa).Trim());

        return 0;
    }
}

