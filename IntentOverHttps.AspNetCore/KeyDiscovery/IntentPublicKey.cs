namespace IntentOverHttps.AspNetCore.KeyDiscovery;

/// <summary>
/// A JWK-compatible public key entry returned by the intent key-discovery endpoint.
/// All coordinate values are Base64Url-encoded.
/// </summary>
/// <param name="Kid">Key identifier.</param>
/// <param name="Kty">Key type (e.g. <c>"EC"</c>).</param>
/// <param name="Crv">Elliptic curve (e.g. <c>"P-256"</c>).</param>
/// <param name="Use">Intended use (e.g. <c>"sig"</c>).</param>
/// <param name="Alg">Algorithm (e.g. <c>"ES256"</c>).</param>
/// <param name="X">Base64Url-encoded X coordinate.</param>
/// <param name="Y">Base64Url-encoded Y coordinate.</param>
public sealed record IntentPublicKey(
    string Kid,
    string Kty,
    string Crv,
    string Use,
    string Alg,
    string X,
    string Y);

