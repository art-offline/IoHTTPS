using System.Text;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Core.Validation;
using IntentOverHttps.Core.Verification;
using IntentOverHttps.Tests.Core.Fakes;

namespace IntentOverHttps.Tests.Core.Verification;

/// <summary>
/// Integration-style tests for <see cref="EcdsaIntentVerifier"/>.
/// A single ECDSA P-256 key pair is generated once per class via the xUnit class fixture.
/// Each test controls time via <see cref="FakeTimeProvider"/> and receives test doubles
/// for key resolution and replay protection.
/// </summary>
public sealed class EcdsaIntentVerifierTests : IClassFixture<EcdsaCryptoFixture>
{
    private const string Issuer = "test-issuer";

    // The intent's issuedAt in every default descriptor.
    // ExpiresAt defaults to BaseTime + 5 minutes.
    private static readonly DateTimeOffset BaseTime = IntentDescriptorFactory.DefaultBaseTime;

    private readonly EcdsaCryptoFixture _fixture;
    private readonly IntentHeaderSerializer _serializer = new();
    private readonly EcdsaIntentVerifier _verifier;

    public EcdsaIntentVerifierTests(EcdsaCryptoFixture fixture)
    {
        _fixture = fixture;
        _verifier = new EcdsaIntentVerifier(_serializer);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Signs the canonical form of <paramref name="descriptor"/> with the fixture key.</summary>
    private byte[] SignDescriptor(IntentDescriptor descriptor)
    {
        var canonical = Encoding.UTF8.GetBytes(_serializer.Serialize(descriptor));
        return _fixture.Sign(canonical);
    }

    /// <summary>
    /// Creates a <see cref="FakeKeyResolver"/> pre-registered with the fixture's public key
    /// under the default <see cref="Issuer"/>.
    /// </summary>
    private FakeKeyResolver CreateKeyResolver()
    {
        var resolver = new FakeKeyResolver();
        resolver.Register(Issuer, _fixture.PublicKeyBytes);
        return resolver;
    }

    /// <summary>
    /// Builds <see cref="IntentVerificationOptions"/> with sensible defaults:
    /// key resolver pre-registered for the test issuer,
    /// fake clock at BaseTime + 2 minutes (inside the valid window),
    /// zero clock skew unless overridden.
    /// </summary>
    private IntentVerificationOptions BuildOptions(
        FakeTimeProvider? timeProvider = null,
        TimeSpan? clockSkew = null,
        Uri? expectedTargetOrigin = null,
        FakeKeyResolver? keyResolver = null,
        FakeReplayProtectionStore? replayStore = null)
        => new(
            keyResolver: keyResolver ?? CreateKeyResolver(),
            replayProtectionStore: replayStore,
            timeProvider: timeProvider ?? new FakeTimeProvider(BaseTime.AddMinutes(2)),
            clockSkew: clockSkew ?? TimeSpan.Zero,
            expectedTargetOrigin: expectedTargetOrigin);

    // ── Verification success ──────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnSuccess_WhenIntentIsValidAndSignatureMatches()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        var options = BuildOptions();

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── Signature verification ────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnSignatureInvalid_WhenPayloadIsModifiedAfterSigning()
    {
        // Arrange — sign the original descriptor, then attempt verification with a tampered one
        var original = IntentDescriptorFactory.CreateValid(issuer: Issuer, amount: 10m);
        var signature = SignDescriptor(original);

        // Tampered: same issuer & nonce, but different amount → different canonical payload
        var tampered = IntentDescriptorFactory.CreateValid(issuer: Issuer, amount: 9999m);
        var options = BuildOptions();

        // Act
        var result = await _verifier.VerifyAsync(tampered, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.SignatureInvalid);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnSignatureInvalid_WhenSignatureBytesAreCorrupted()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        signature[0] ^= 0xFF; // flip bits to corrupt the signature
        var options = BuildOptions();

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.SignatureInvalid);
    }

    [Fact]
    public async Task VerifyAsync_ShouldSkipSignatureCheck_WhenNoKeyResolverIsProvided()
    {
        // Arrange — deliberately pass an empty signature with no resolver
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var options = new IntentVerificationOptions(
            keyResolver: null,
            timeProvider: new FakeTimeProvider(BaseTime.AddMinutes(2)),
            clockSkew: TimeSpan.Zero);

        // Act
        var result = await _verifier.VerifyAsync(descriptor, ReadOnlyMemory<byte>.Empty, options);

        // Assert — no key resolver means signature check is simply skipped
        Assert.True(result.IsValid);
    }

    // ── Expiration ────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnExpired_WhenCurrentTimeIsAfterExpiresAtPlusClockSkew()
    {
        // Arrange
        // issuedAt  = BaseTime            expiresAt = BaseTime + 5 min
        // clockSkew = 30 s   →  window ends at BaseTime + 5:30
        // now       = BaseTime + 5:31   →  past the window → Expired
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        var now = BaseTime.AddMinutes(5).AddSeconds(31);
        var options = BuildOptions(
            timeProvider: new FakeTimeProvider(now),
            clockSkew: TimeSpan.FromSeconds(30));

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.Expired);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnSuccess_WhenIntentIsJustExpiredButStillWithinClockSkewWindow()
    {
        // Arrange
        // expiresAt = BaseTime + 5 min
        // clockSkew = 30 s   →  window ends at BaseTime + 5:30
        // now       = BaseTime + 5:29   →  inside the window → accepted
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        var now = BaseTime.AddMinutes(5).AddSeconds(29);
        var options = BuildOptions(
            timeProvider: new FakeTimeProvider(now),
            clockSkew: TimeSpan.FromSeconds(30));

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnNotYetValid_WhenCurrentTimeIsBeforeIssuedAtMinusClockSkew()
    {
        // Arrange
        // issuedAt  = BaseTime
        // clockSkew = 30 s   →  acceptance starts at BaseTime − 30 s
        // now       = BaseTime − 31 s   →  before the window → NotYetValid
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        var now = BaseTime.AddSeconds(-31);
        var options = BuildOptions(
            timeProvider: new FakeTimeProvider(now),
            clockSkew: TimeSpan.FromSeconds(30));

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.NotYetValid);
    }

    // ── Origin mismatch ───────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnInvalidTargetOrigin_WhenOriginDoesNotMatchExpected()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(
            issuer: Issuer,
            targetOrigin: new Uri("https://merchant.example"));
        var signature = SignDescriptor(descriptor);
        var options = BuildOptions(
            expectedTargetOrigin: new Uri("https://other-merchant.example"));

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Code == IntentErrorCode.InvalidTargetOrigin && e.Field == "targetOrigin");
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnSuccess_WhenOriginMatchesExpectedExactly()
    {
        // Arrange
        var origin = new Uri("https://merchant.example");
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer, targetOrigin: origin);
        var signature = SignDescriptor(descriptor);
        var options = BuildOptions(expectedTargetOrigin: origin);

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.True(result.IsValid);
    }

    // ── Key resolution ────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnKeyNotFound_WhenIssuerHasNoRegisteredPublicKey()
    {
        // Arrange — descriptor claims a different issuer than the one registered
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: "unknown-issuer");
        var signature = SignDescriptor(descriptor);
        // resolver only knows "test-issuer"
        var options = BuildOptions(keyResolver: CreateKeyResolver());

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.KeyNotFound);
    }

    // ── Replay protection ─────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldReturnReplayDetected_WhenSameIntentIsSubmittedTwice()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var signature = SignDescriptor(descriptor);
        var replayStore = new FakeReplayProtectionStore();
        var options = BuildOptions(replayStore: replayStore);

        // Act
        var firstResult = await _verifier.VerifyAsync(descriptor, signature, options);
        var secondResult = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert
        Assert.True(firstResult.IsValid);
        Assert.False(secondResult.IsValid);
        Assert.Contains(secondResult.Errors, e => e.Code == IntentErrorCode.ReplayDetected);
    }

    [Fact]
    public async Task VerifyAsync_ShouldNotConsumeNonce_WhenSignatureIsInvalid()
    {
        // Arrange — a tampered signature must not poison the replay store,
        // otherwise a subsequent valid request would be incorrectly rejected.
        var descriptor = IntentDescriptorFactory.CreateValid(issuer: Issuer);
        var validSignature = SignDescriptor(descriptor);

        var corruptedSignature = (byte[])validSignature.Clone();
        corruptedSignature[0] ^= 0xFF;

        var replayStore = new FakeReplayProtectionStore();
        var options = BuildOptions(replayStore: replayStore);

        // Act — first call with the corrupted signature
        var tamperedResult = await _verifier.VerifyAsync(descriptor, corruptedSignature, options);

        // Act — second call with the legitimate signature
        var validResult = await _verifier.VerifyAsync(descriptor, validSignature, options);

        // Assert
        Assert.False(tamperedResult.IsValid);
        Assert.Contains(tamperedResult.Errors, e => e.Code == IntentErrorCode.SignatureInvalid);

        Assert.True(validResult.IsValid); // nonce was NOT consumed by the invalid attempt
        Assert.Equal(1, replayStore.StoredCount);
    }

    // ── Error accumulation ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ShouldAccumulateMultipleErrors_WhenIntentIsExpiredAndOriginMismatches()
    {
        // Arrange — well past expiresAt AND wrong origin
        var descriptor = IntentDescriptorFactory.CreateValid(
            issuer: Issuer,
            targetOrigin: new Uri("https://merchant.example"));
        var signature = SignDescriptor(descriptor);
        var options = BuildOptions(
            clockSkew: TimeSpan.Zero,
            timeProvider: new FakeTimeProvider(BaseTime.AddMinutes(10)), // past expiresAt (5 min)
            expectedTargetOrigin: new Uri("https://other.example"));

        // Act
        var result = await _verifier.VerifyAsync(descriptor, signature, options);

        // Assert — both errors are present simultaneously
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.Expired);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.InvalidTargetOrigin);
        Assert.True(result.Errors.Count >= 2);
    }
}

