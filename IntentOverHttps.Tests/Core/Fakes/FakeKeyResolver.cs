using IntentOverHttps.Core.Abstractions;

namespace IntentOverHttps.Tests.Core.Fakes;

/// <summary>
/// An in-memory <see cref="IKeyResolver"/> backed by a dictionary keyed on issuer name.
/// </summary>
internal sealed class FakeKeyResolver : IKeyResolver
{
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>Registers a public key (SubjectPublicKeyInfo bytes) for the given issuer.</summary>
    internal void Register(string issuer, byte[] publicKeyBytes)
        => _keys[issuer] = publicKeyBytes.ToArray();

    public ValueTask<byte[]?> ResolveKeyAsync(
        string issuer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _keys.TryGetValue(issuer, out var key) ? (byte[]?)key.ToArray() : null);
    }
}

