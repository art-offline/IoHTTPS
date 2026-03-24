using IntentOverHttps.Core.Validation;

namespace IntentOverHttps.Cli.ConsoleOutput;

internal static class ConsolePrinter
{
    public static void WriteSection(string title)
    {
        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));
    }

    public static void WriteKeyValue(string label, string? value)
    {
        if (value is null)
        {
            Console.WriteLine($"{label}: <null>");
            return;
        }

        if (value.Contains(Environment.NewLine, StringComparison.Ordinal) || value.Contains('\n'))
        {
            Console.WriteLine($"{label}:");
            Console.WriteLine(value);
            return;
        }

        Console.WriteLine($"{label}: {value}");
    }

    public static void WriteLine(string? value = null) => Console.WriteLine(value);

    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteValidationResult(IntentValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsValid)
        {
            WriteSuccess("Validation result: VALID");
            return;
        }

        WriteError("Validation result: INVALID");
        Console.WriteLine();
        Console.WriteLine("Errors:");
        foreach (var error in result.Errors)
        {
            WriteValidationError(error);
        }
    }

    private static void WriteValidationError(IntentValidationError error)
    {
        var fieldSuffix = string.IsNullOrWhiteSpace(error.Field)
            ? string.Empty
            : $" (field: {error.Field})";

        Console.WriteLine($"  - [{error.Code}]{fieldSuffix} {error.Message}");
    }
}

