namespace IntentOverHttps.DemoWeb.Contracts;

public sealed record IntentKeysResponse(
    string Issuer,
    string Version,
    IReadOnlyList<JsonWebKeyResponse> Keys);

public sealed record JsonWebKeyResponse(
    string Kid,
    string Kty,
    string Crv,
    string Use,
    string Alg,
    string X,
    string Y);

