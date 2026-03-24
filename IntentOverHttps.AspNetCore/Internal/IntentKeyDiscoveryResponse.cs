using System.Text.Json.Serialization;
using IntentOverHttps.AspNetCore.KeyDiscovery;

namespace IntentOverHttps.AspNetCore.Internal;

/// <summary>Wire format for the <c>/.well-known/intent-keys</c> response.</summary>
internal sealed record IntentKeyDiscoveryResponse(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("keys")] IReadOnlyList<IntentPublicKeyJson> Keys);

/// <summary>JSON representation of a single JWK entry.</summary>
internal sealed record IntentPublicKeyJson(
    [property: JsonPropertyName("kid")] string Kid,
    [property: JsonPropertyName("kty")] string Kty,
    [property: JsonPropertyName("crv")] string Crv,
    [property: JsonPropertyName("use")] string Use,
    [property: JsonPropertyName("alg")] string Alg,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y);

