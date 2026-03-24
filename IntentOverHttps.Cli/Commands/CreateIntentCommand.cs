using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Parsing;
using IntentOverHttps.Core.Serialization;

namespace IntentOverHttps.Cli.Commands;

internal sealed class CreateIntentCommand : ICliCommand
{
    private readonly IntentHeaderSerializer _serializer = new();

    public string Name => "create-intent";

    public string Summary => "Create and serialize an IoHTTPS intent descriptor.";

    public string HelpText => $"""
Usage:
  IntentOverHttps.Cli create-intent [options]

{IntentCommandSupport.IntentOptionsHelp}
Options:
  --help            Show command help.

Description:
  Builds an IntentDescriptor from command-line values and prints the canonical
  serialized IoHTTPS header value.
""";

    public Task<int> ExecuteAsync(CommandArguments arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = IntentCommandSupport.CreateIntentDescriptor(arguments);
        var canonical = _serializer.Serialize(descriptor);

        ConsolePrinter.WriteSection("Canonical Intent Header");
        Console.WriteLine(canonical);
        ConsolePrinter.WriteLine();

        ConsolePrinter.WriteSection("Intent Summary");
        ConsolePrinter.WriteKeyValue("Action", descriptor.Action);
        ConsolePrinter.WriteKeyValue("Issuer", descriptor.Issuer);
        ConsolePrinter.WriteKeyValue("Target Origin", descriptor.TargetOrigin.AbsoluteUri);
        ConsolePrinter.WriteKeyValue("Beneficiary", descriptor.Beneficiary);
        ConsolePrinter.WriteKeyValue("Amount", descriptor.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ConsolePrinter.WriteKeyValue("Currency", descriptor.Currency);
        ConsolePrinter.WriteKeyValue("Issued At", descriptor.IssuedAt.ToString("O"));
        ConsolePrinter.WriteKeyValue("Expires At", descriptor.ExpiresAt.ToString("O"));
        ConsolePrinter.WriteKeyValue("Nonce", descriptor.Nonce);

        return Task.FromResult(0);
    }
}

