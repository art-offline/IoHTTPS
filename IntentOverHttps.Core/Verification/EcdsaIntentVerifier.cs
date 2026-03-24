using System.Security.Cryptography;
using System.Text;
using IntentOverHttps.Core.Abstractions;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Core.Validation;

namespace IntentOverHttps.Core.Verification;

public sealed class EcdsaIntentVerifier : IIntentVerifier
{
    private readonly IntentHeaderSerializer _serializer;

    public EcdsaIntentVerifier(IntentHeaderSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializer = serializer;
    }

    public async ValueTask<IntentValidationResult> VerifyAsync(
        IntentDescriptor intent,
        ReadOnlyMemory<byte> signature,
        IntentVerificationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<IntentValidationError>();

        CheckOrigin(intent, options, errors);
        CheckTemporalValidity(intent, options, errors);
        await CheckSignatureAsync(intent, signature, options, errors, cancellationToken);
        await CheckReplayAsync(intent, options, errors, cancellationToken);

        return errors.Count == 0
            ? IntentValidationResult.Success
            : IntentValidationResult.Failure(errors);
    }

    private static void CheckOrigin(
        IntentDescriptor intent,
        IntentVerificationOptions options,
        List<IntentValidationError> errors)
    {
        if (options.ExpectedTargetOrigin is null) return;

        var isMatch = Uri.Compare(
            intent.TargetOrigin,
            options.ExpectedTargetOrigin,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

        if (!isMatch)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidTargetOrigin,
                $"Intent target origin '{intent.TargetOrigin}' does not match the expected origin '{options.ExpectedTargetOrigin}'.",
                "targetOrigin"));
        }
    }

    private static void CheckTemporalValidity(
        IntentDescriptor intent,
        IntentVerificationOptions options,
        List<IntentValidationError> errors)
    {
        var now = options.TimeProvider.GetUtcNow();

        // Allow clock skew on the receiving side: accept until expiresAt + skew.
        if (intent.ExpiresAt.Add(options.ClockSkew) < now)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.Expired,
                $"Intent expired at {intent.ExpiresAt:O}."));
        }

        // Reject intents whose issuedAt is too far in the future.
        if (intent.IssuedAt.Subtract(options.ClockSkew) > now)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.NotYetValid,
                $"Intent is not yet valid; it was issued at {intent.IssuedAt:O}."));
        }
    }

    private async ValueTask CheckSignatureAsync(
        IntentDescriptor intent,
        ReadOnlyMemory<byte> signature,
        IntentVerificationOptions options,
        List<IntentValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (options.KeyResolver is null) return;

        var publicKeyBytes = await options.KeyResolver.ResolveKeyAsync(intent.Issuer, cancellationToken);
        if (publicKeyBytes is null)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.KeyNotFound,
                $"No public key found for issuer '{intent.Issuer}'."));
            return;
        }

        var canonical = Encoding.UTF8.GetBytes(_serializer.Serialize(intent));

        using var ecKey = ECDsa.Create();
        ecKey.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

        if (!ecKey.VerifyData(canonical, signature.Span, HashAlgorithmName.SHA256))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.SignatureInvalid,
                "Intent signature verification failed."));
        }
    }

    private static async ValueTask CheckReplayAsync(
        IntentDescriptor intent,
        IntentVerificationOptions options,
        List<IntentValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (options.ReplayProtectionStore is null) return;

        // Only consume the nonce when the intent is otherwise valid.
        // This prevents an attacker from poisoning the replay store
        // with forged or expired intents.
        if (errors.Count > 0) return;

        var stored = await options.ReplayProtectionStore.TryStoreAsync(
            intent.Issuer,
            intent.Nonce,
            intent.ExpiresAt,
            cancellationToken);

        if (!stored)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.ReplayDetected,
                $"Intent with nonce '{intent.Nonce}' from issuer '{intent.Issuer}' has already been processed."));
        }
    }
}

