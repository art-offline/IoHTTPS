using IntentOverHttps.Cli.Parsing;

namespace IntentOverHttps.Cli.Commands;

internal interface ICliCommand
{
    string Name { get; }

    string Summary { get; }

    string HelpText { get; }

    Task<int> ExecuteAsync(CommandArguments arguments, CancellationToken cancellationToken);
}

