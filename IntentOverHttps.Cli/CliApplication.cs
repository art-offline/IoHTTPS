using IntentOverHttps.Cli.Commands;
using IntentOverHttps.Cli.ConsoleOutput;
using IntentOverHttps.Cli.Parsing;

namespace IntentOverHttps.Cli;

internal sealed class CliApplication
{
    private readonly IReadOnlyDictionary<string, ICliCommand> _commands;

    public CliApplication(IEnumerable<ICliCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands.ToDictionary(static command => command.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            PrintRootHelp();
            return 0;
        }

        var commandName = args[0];
        if (string.Equals(commandName, "help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandName, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandName, "-h", StringComparison.OrdinalIgnoreCase))
        {
            PrintRootHelp();
            return 0;
        }

        if (!_commands.TryGetValue(commandName, out var command))
        {
            ConsolePrinter.WriteError($"Unknown command '{commandName}'.");
            ConsolePrinter.WriteLine();
            PrintRootHelp();
            return 1;
        }

        try
        {
            var parsedArguments = CommandArguments.Parse(commandName, args[1..]);
            if (parsedArguments.IsHelpRequested)
            {
                Console.WriteLine(command.HelpText);
                return 0;
            }

            return await command.ExecuteAsync(parsedArguments, cancellationToken);
        }
        catch (CommandUsageException ex)
        {
            ConsolePrinter.WriteError(ex.Message);
            ConsolePrinter.WriteLine();
            Console.WriteLine(command.HelpText);
            return 1;
        }
        catch (OperationCanceledException)
        {
            ConsolePrinter.WriteError("Operation cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            ConsolePrinter.WriteError(ex.Message);
            return 1;
        }
    }

    private void PrintRootHelp()
    {
        Console.WriteLine("IntentOverHttps CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  IntentOverHttps.Cli <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        foreach (var command in _commands.Values.OrderBy(static command => command.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {command.Name,-14} {command.Summary}");
        }

        Console.WriteLine();
        Console.WriteLine("Run 'IntentOverHttps.Cli <command> --help' for detailed usage.");
    }
}

