namespace IntentOverHttps.Cli.Parsing;

internal sealed class CommandUsageException : Exception
{
    public CommandUsageException(string message)
        : base(message)
    {
    }
}

