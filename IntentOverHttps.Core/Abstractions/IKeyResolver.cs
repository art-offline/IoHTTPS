namespace IntentOverHttps.Core.Abstractions;

public interface IKeyResolver
{
    ValueTask<byte[]?> ResolveKeyAsync(string issuer, CancellationToken cancellationToken = default);
}

