namespace IntentOverHttps.AspNetCore.Internal;

/// <summary>
/// Base64Url encoding/decoding helpers (RFC 4648 §5, no padding).
/// </summary>
internal static class Base64Url
{
    internal static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static byte[] Decode(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var paddingLength = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + paddingLength, '=');
        return Convert.FromBase64String(normalized);
    }
}

