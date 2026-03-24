namespace IntentOverHttps.AspNetCore.Signing;

/// <summary>
/// Describes the active signing key: its identifier and algorithm.
/// Used to populate <c>Intent-Key-Id</c> and <c>Intent-Alg</c> response headers.
/// </summary>
/// <param name="KeyId">Identifier matching the key published at <c>/.well-known/intent-keys</c>.</param>
/// <param name="Algorithm">Algorithm identifier, e.g. <c>"ES256"</c>.</param>
public sealed record IntentKeyMetadata(string KeyId, string Algorithm);

