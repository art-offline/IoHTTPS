namespace IntentOverHttps.Cli.Crypto;

internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static byte[] Decode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var paddingLength = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + paddingLength, '=');

        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Signature must be valid Base64Url.", nameof(value), ex);
        }
    }
}

