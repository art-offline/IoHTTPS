using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Models;

namespace IntentOverHttps.Tests.AspNetCore.Fakes;

/// <summary>
/// Test double for <see cref="IIntentSigner"/> that returns a fixed signature.
/// </summary>
internal sealed class FakeIntentSigner : IIntentSigner
{
    private readonly byte[] _signatureBytes;

    public FakeIntentSigner(byte[]? signatureBytes = null)
    {
        _signatureBytes = signatureBytes ?? [0xDE, 0xAD, 0xBE, 0xEF];
    }

    public int CallCount { get; private set; }
    public IntentDescriptor? LastDescriptor { get; private set; }

    public ValueTask<byte[]> SignAsync(IntentDescriptor intent, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastDescriptor = intent;
        return ValueTask.FromResult(_signatureBytes);
    }
}

