namespace IntentOverHttps.DemoWeb.Services;

public sealed record PublishedIntentKey(
    string Kid,
    string Kty,
    string Crv,
    string Use,
    string Alg,
    string X,
    string Y);

