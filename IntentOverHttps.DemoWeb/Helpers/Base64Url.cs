namespace IntentOverHttps.DemoWeb.Helpers;

internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Decode(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var paddingLength = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + paddingLength, '=');
        return Convert.FromBase64String(normalized);
    }
}

