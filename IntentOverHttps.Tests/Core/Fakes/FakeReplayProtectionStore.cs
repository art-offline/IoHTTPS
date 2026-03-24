using IntentOverHttps.Core.Abstractions;

namespace IntentOverHttps.Tests.Core.Fakes;

/// <summary>
/// An in-memory <see cref="IReplayProtectionStore"/> that uses a HashSet to track
/// (issuer, nonce) pairs seen so far. Returns <c>false</c> on the second submission.
/// </summary>
internal sealed class FakeReplayProtectionStore : IReplayProtectionStore
{
    private readonly HashSet<(string Issuer, string Nonce)> _seen = [];

    public ValueTask<bool> TryStoreAsync(
        string issuer,
        string nonce,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_seen.Add((issuer, nonce)));
    }

    /// <summary>Returns how many unique (issuer, nonce) pairs have been stored.</summary>
    internal int StoredCount => _seen.Count;
}

