using IntentOverHttps.Core.Models;

namespace IntentOverHttps.Core.Abstractions;

public interface IIntentSigner
{
    ValueTask<byte[]> SignAsync(IntentDescriptor intent, CancellationToken cancellationToken = default);
}

