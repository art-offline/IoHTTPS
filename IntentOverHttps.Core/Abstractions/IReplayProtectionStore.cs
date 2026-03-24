namespace IntentOverHttps.Core.Abstractions;

public interface IReplayProtectionStore
{
    ValueTask<bool> TryStoreAsync(
        string issuer,
        string nonce,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

