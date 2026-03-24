using System.Globalization;

namespace IntentOverHttps.Cli.Parsing;

internal sealed class CommandArguments
{
    private readonly IReadOnlyDictionary<string, string?> _options;

    private CommandArguments(string commandName, IReadOnlyDictionary<string, string?> options)
    {
        CommandName = commandName;
        _options = options;
    }

    public string CommandName { get; }

    public bool IsHelpRequested => HasFlag("help") || HasFlag("h");

    public static CommandArguments Parse(string commandName, string[] args)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(args);

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];

            if (string.Equals(token, "--", StringComparison.Ordinal))
            {
                throw new CommandUsageException("Positional arguments are not supported.");
            }

            if (string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase))
            {
                options[NormalizeName("help")] = null;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CommandUsageException($"Unexpected argument '{token}'. Options must use the form --name value or --name=value.");
            }

            var optionToken = token[2..];
            if (optionToken.Length == 0)
            {
                throw new CommandUsageException("Encountered an empty option name.");
            }

            string name;
            string? value;
            var separatorIndex = optionToken.IndexOf('=');
            if (separatorIndex >= 0)
            {
                name = optionToken[..separatorIndex];
                value = optionToken[(separatorIndex + 1)..];
            }
            else
            {
                name = optionToken;
                if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++index];
                }
                else
                {
                    value = null;
                }
            }

            name = NormalizeName(name);
            if (name.Length == 0)
            {
                throw new CommandUsageException("Encountered an empty option name.");
            }

            if (!options.TryAdd(name, value))
            {
                throw new CommandUsageException($"Option '--{name}' was specified more than once.");
            }
        }

        return new CommandArguments(commandName, options);
    }

    public bool HasOption(string name) => _options.ContainsKey(NormalizeName(name));

    public bool HasFlag(string name)
    {
        if (!_options.TryGetValue(NormalizeName(name), out var value))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(value) || bool.TryParse(value, out var result) && result;
    }

    public string GetRequired(string name)
    {
        var value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CommandUsageException($"Missing required option '--{NormalizeName(name)}'.");
        }

        return value;
    }

    public string? GetOptional(string name)
    {
        return _options.TryGetValue(NormalizeName(name), out var value)
            ? value
            : null;
    }

    public int? GetOptionalInt32(string name)
    {
        var value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new CommandUsageException($"Option '--{NormalizeName(name)}' must be a valid integer.");
        }

        return result;
    }

    public decimal GetRequiredDecimal(string name)
    {
        var value = GetRequired(name);
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            throw new CommandUsageException($"Option '--{NormalizeName(name)}' must be a valid decimal number using invariant culture (example: 12.34).");
        }

        return result;
    }

    public DateTimeOffset? GetOptionalDateTimeOffset(string name)
    {
        var value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            throw new CommandUsageException($"Option '--{NormalizeName(name)}' must be a valid ISO-8601 timestamp (example: 2026-03-24T12:00:00Z).");
        }

        return result;
    }

    public Uri? GetOptionalAbsoluteUri(string name)
    {
        var value = GetOptional(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var result))
        {
            throw new CommandUsageException($"Option '--{NormalizeName(name)}' must be an absolute URI.");
        }

        return result;
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();
}

