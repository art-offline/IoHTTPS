using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Crypto;
using IntentOverHttps.Cli.Parsing;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Core.Verification;

namespace IntentOverHttps.Cli.Commands;

internal sealed class VerifyIntentCommand : ICliCommand
{
    private readonly IntentHeaderParser _parser = new();
    private readonly EcdsaIntentVerifier _verifier;

    public VerifyIntentCommand()
    {
        _verifier = new EcdsaIntentVerifier(new IntentHeaderSerializer());
    }

    public string Name => "verify-intent";

    public string Summary => "Verify a serialized intent and signature using a public key.";

    public string HelpText => """
Usage:
  IntentOverHttps.Cli verify-intent [options]

Options:
  --intent <value>                 Required serialized Intent header value.
  --signature <value>              Required Base64Url signature from Intent-Signature.
  --public-key-file <path>         Required unless --public-key is provided.
  --public-key <value>             Inline PEM or Base64 SubjectPublicKeyInfo value.
  --trusted-issuer <value>         Optional. If provided, the public key is only used for that issuer.
  --expected-target-origin <uri>   Optional expected origin check.
  --clock-skew-seconds <seconds>   Optional. Default: 300
  --help                           Show command help.

Description:
  Parses the canonical intent header, decodes the signature, verifies the ECDSA
  signature using the provided public key, and prints validation errors clearly.
""";

    public async Task<int> ExecuteAsync(CommandArguments arguments, CancellationToken cancellationToken)
    {
        var intentHeader = arguments.GetRequired("intent");
        var signatureHeader = arguments.GetRequired("signature");
        var publicKeyMaterial = ReadKeyMaterial(arguments, "public-key-file", "public-key", "public key");

        var parseResult = _parser.Parse(intentHeader, out var descriptor);
        if (!parseResult.IsValid || descriptor is null)
        {
            ConsolePrinter.WriteSection("Parsed Intent");
            ConsolePrinter.WriteError("The provided intent header is not valid.");
            ConsolePrinter.WriteValidationResult(parseResult);
            return 1;
        }

        byte[] signatureBytes;
        byte[] publicKeySpki;

        try
        {
            signatureBytes = Base64Url.Decode(signatureHeader);
            using var publicKey = PemCodec.ImportPublicKey(publicKeyMaterial);
            publicKeySpki = PemCodec.ExportPublicKeySpki(publicKey);
        }
        catch (ArgumentException ex)
        {
            throw new CommandUsageException(ex.Message);
        }

        var clockSkewSeconds = arguments.GetOptionalInt32("clock-skew-seconds") ?? 300;
        if (clockSkewSeconds < 0)
        {
            throw new CommandUsageException("Option '--clock-skew-seconds' must be greater than or equal to zero.");
        }

        var trustedIssuer = arguments.GetOptional("trusted-issuer");
        var expectedOrigin = arguments.GetOptionalAbsoluteUri("expected-target-origin");
        var verificationOptions = new IntentVerificationOptions(
            keyResolver: new SingleKeyResolver(publicKeySpki, trustedIssuer),
            clockSkew: TimeSpan.FromSeconds(clockSkewSeconds),
            expectedTargetOrigin: expectedOrigin);

        var result = await _verifier.VerifyAsync(descriptor, signatureBytes, verificationOptions, cancellationToken);

        ConsolePrinter.WriteSection("Parsed Intent");
        ConsolePrinter.WriteKeyValue("Action", descriptor.Action);
        ConsolePrinter.WriteKeyValue("Issuer", descriptor.Issuer);
        ConsolePrinter.WriteKeyValue("Target Origin", descriptor.TargetOrigin.AbsoluteUri);
        ConsolePrinter.WriteKeyValue("Nonce", descriptor.Nonce);
        ConsolePrinter.WriteLine();
        ConsolePrinter.WriteValidationResult(result);

        return result.IsValid ? 0 : 1;
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

