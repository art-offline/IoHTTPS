namespace IntentOverHttps.AspNetCore;

/// <summary>
/// String constants for all IoHTTPS protocol headers.
/// </summary>
public static class IntentHeaderNames
{
    /// <summary>The canonical serialized intent payload.</summary>
    public const string Intent = "Intent";

    /// <summary>Base64Url-encoded ECDSA signature over the canonical intent.</summary>
    public const string Signature = "Intent-Signature";

    /// <summary>Identifier of the signing key used to produce the signature.</summary>
    public const string KeyId = "Intent-Key-Id";

    /// <summary>Signing algorithm identifier (e.g. "ES256").</summary>
    public const string Algorithm = "Intent-Alg";

    /// <summary>Protocol version (e.g. "1").</summary>
    public const string Version = "Intent-Version";
}

