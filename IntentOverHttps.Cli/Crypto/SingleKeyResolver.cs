using IntentOverHttps.Core.Abstractions;

namespace IntentOverHttps.Cli.Crypto;

internal sealed class SingleKeyResolver : IKeyResolver
{
    private readonly byte[] _publicKeySpki;
    private readonly string? _trustedIssuer;

    public SingleKeyResolver(byte[] publicKeySpki, string? trustedIssuer = null)
    {
        ArgumentNullException.ThrowIfNull(publicKeySpki);
        _publicKeySpki = publicKeySpki.ToArray();
        _trustedIssuer = string.IsNullOrWhiteSpace(trustedIssuer)
            ? null
            : trustedIssuer.Trim();
    }

    public ValueTask<byte[]?> ResolveKeyAsync(string issuer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_trustedIssuer is not null && !string.Equals(issuer, _trustedIssuer, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<byte[]?>(null);
        }

        return ValueTask.FromResult<byte[]?>(_publicKeySpki.ToArray());
    }
}

