namespace IntentOverHttps.DemoWeb.Contracts;

public sealed record PayDemoResponse(
    string Message,
    string Issuer,
    string Action,
    string Beneficiary,
    decimal Amount,
    string Currency,
    string Nonce,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string IntentKeysUrl);

