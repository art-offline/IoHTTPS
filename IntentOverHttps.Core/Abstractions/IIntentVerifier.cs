using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Validation;

namespace IntentOverHttps.Core.Abstractions;

public interface IIntentVerifier
{
    ValueTask<IntentValidationResult> VerifyAsync(
        IntentDescriptor intent,
        ReadOnlyMemory<byte> signature,
        global::IntentOverHttps.Core.Verification.IntentVerificationOptions options,
        CancellationToken cancellationToken = default);
}

