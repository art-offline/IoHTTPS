namespace IntentOverHttps.AspNetCore.Signing;

/// <summary>
/// Returns metadata about the currently active signing key so that
/// <c>Intent-Key-Id</c> and <c>Intent-Alg</c> headers can be populated correctly.
/// Implement this interface and register it in DI when calling
/// <c>AddIntentOverHttps()</c>.
/// </summary>
public interface IIntentKeyMetadataProvider
{
    /// <summary>
    /// Returns the key identifier and algorithm for the active signing key.
    /// Async to support runtime key rotation (e.g. Azure Key Vault).
    /// </summary>
    ValueTask<IntentKeyMetadata> GetCurrentKeyMetadataAsync(CancellationToken cancellationToken = default);
}

