using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Crypto;
using IntentOverHttps.Cli.Parsing;
using IntentOverHttps.Core.Serialization;

namespace IntentOverHttps.Cli.Commands;

internal sealed class SignIntentCommand : ICliCommand
{
    private readonly IntentHeaderSerializer _serializer = new();

    public string Name => "sign-intent";

    public string Summary => "Create and sign an IoHTTPS intent using a private key.";

    public string HelpText => $"""
Usage:
  IntentOverHttps.Cli sign-intent [options]

{IntentCommandSupport.IntentOptionsHelp}
Key options:
  --private-key-file <path>   Required unless --private-key is provided.
  --private-key <value>       Inline PKCS#8 PEM or Base64 PKCS#8 value.
  --key-id <value>            Optional. Defaults to a dev-friendly id derived from the public key.
  --algorithm <value>         Optional. Default: ES256
  --version <value>           Optional. Default: 1
  --help                      Show command help.

Description:
  Builds an intent, signs it with the provided private key, and prints the
  five IoHTTPS headers ready to copy into tests or debugging sessions.
""";

    public async Task<int> ExecuteAsync(CommandArguments arguments, CancellationToken cancellationToken)
    {
        var descriptor = IntentCommandSupport.CreateIntentDescriptor(arguments);
        var privateKeyMaterial = ReadKeyMaterial(arguments, "private-key-file", "private-key", "private key");

        using var signingKey = PemCodec.ImportPrivateKey(privateKeyMaterial);
        var signer = new LocalEcdsaIntentSigner(signingKey, _serializer);

        var canonical = _serializer.Serialize(descriptor);
        var signature = await signer.SignAsync(descriptor, cancellationToken);
        var publicSpki = PemCodec.ExportPublicKeySpki(signingKey);
        var keyId = arguments.GetOptional("key-id") ?? PemCodec.CreateKeyId(publicSpki);
        var algorithm = arguments.GetOptional("algorithm") ?? "ES256";
        var version = arguments.GetOptional("version") ?? "1";
        var signatureHeader = Base64Url.Encode(signature);

        ConsolePrinter.WriteSection("IoHTTPS Headers");
        Console.WriteLine($"{IntentHeaderNames.Intent}: {canonical}");
        Console.WriteLine($"{IntentHeaderNames.Signature}: {signatureHeader}");
        Console.WriteLine($"{IntentHeaderNames.KeyId}: {keyId}");
        Console.WriteLine($"{IntentHeaderNames.Algorithm}: {algorithm}");
        Console.WriteLine($"{IntentHeaderNames.Version}: {version}");
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Verification Inputs");
        ConsolePrinter.WriteKeyValue("Intent", canonical);
        ConsolePrinter.WriteKeyValue("Signature", signatureHeader);
        ConsolePrinter.WriteKeyValue("Public Key (SPKI PEM)", PemCodec.ExportPublicKeyPem(signingKey).Trim());

        return 0;
    }

    private static string ReadKeyMaterial(CommandArguments arguments, string fileOption, string inlineOption, string displayName)
    {
        var filePath = arguments.GetOptional(fileOption);
        var inlineValue = arguments.GetOptional(inlineOption);

        if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(inlineValue))
        {
            throw new CommandUsageException($"Provide either '--{fileOption}' or '--{inlineOption}' for the {displayName}.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(inlineValue))
        {
            throw new CommandUsageException($"Use either '--{fileOption}' or '--{inlineOption}', not both.");
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                throw new CommandUsageException($"Unable to read {displayName} file '{filePath}': {ex.Message}");
            }
        }

        return inlineValue!;
    }
}

