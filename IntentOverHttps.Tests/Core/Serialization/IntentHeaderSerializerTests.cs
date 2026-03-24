using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Tests.Core.Fakes;

namespace IntentOverHttps.Tests.Core.Serialization;

public sealed class IntentHeaderSerializerTests
{
    private readonly IntentHeaderSerializer _serializer = new();

    // ── Canonical format ──────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ShouldProduceDeterministicCanonicalFormat()
    {
        // Arrange
        var descriptor = new IntentDescriptor(
            action: "authorize",
            issuer: "wallet-service",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "merchant=42;acct\\main",
            amount: 12.34m,
            currency: "eur",
            issuedAt: new DateTimeOffset(2026, 03, 24, 10, 15, 30, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 03, 24, 10, 20, 30, TimeSpan.Zero),
            nonce: "abc=123;xyz\\");

        // Act
        var serializedOnce = _serializer.Serialize(descriptor);
        var serializedTwice = _serializer.Serialize(descriptor);

        // Assert — same descriptor always yields the same string
        Assert.Equal(serializedOnce, serializedTwice);
        Assert.Equal(
            "action=authorize;issuer=wallet-service;targetOrigin=https://merchant.example;beneficiary=merchant\\=42\\;acct\\\\main;amount=12.34;currency=EUR;issuedAt=2026-03-24T10:15:30.0000000+00:00;expiresAt=2026-03-24T10:20:30.0000000+00:00;nonce=abc\\=123\\;xyz\\\\",
            serializedOnce);
    }

    // ── Amount formatting ─────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ShouldFormatIntegerAmountWithoutDecimalPoint()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(amount: 100m);

        // Act
        var header = _serializer.Serialize(descriptor);

        // Assert — no trailing ".0" or decimal part
        Assert.Contains("amount=100;", header);
    }

    [Fact]
    public void Serialize_ShouldPreserveSignificantDecimalPlaces_InAmount()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(amount: 9.99m);

        // Act
        var header = _serializer.Serialize(descriptor);

        // Assert
        Assert.Contains("amount=9.99;", header);
    }

    // ── Currency normalisation ────────────────────────────────────────────────

    [Fact]
    public void Serialize_ShouldOutputCurrencyInUpperCase_RegardlessOfInput()
    {
        // Arrange
        var descriptor = IntentDescriptorFactory.CreateValid(currency: "usd");

        // Act
        var header = _serializer.Serialize(descriptor);

        // Assert — lowercase input always serialized as uppercase
        Assert.Contains("currency=USD;", header);
        Assert.DoesNotContain("currency=usd", header);
    }

    // ── Timestamp format ──────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ShouldOutputTimestampsInUtcRoundTripFormat_WhenInputHasOffset()
    {
        // Arrange — issuedAt with +02:00 offset = 10:00 UTC
        var issued = new DateTimeOffset(2026, 3, 24, 12, 0, 0, TimeSpan.FromHours(2));
        var expires = issued.AddMinutes(5);
        var descriptor = IntentDescriptorFactory.CreateValid(issuedAt: issued, expiresAt: expires);

        // Act
        var header = _serializer.Serialize(descriptor);

        // Assert — UTC representation in round-trip "O" format (+00:00)
        Assert.Contains("issuedAt=2026-03-24T10:00:00.0000000+00:00;", header);
        Assert.Contains("expiresAt=2026-03-24T10:05:00.0000000+00:00;", header);
    }

    // ── Escape sequences ──────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ShouldEscapeSpecialCharacters_InAllVariableStringFields()
    {
        // Arrange — embed all three special chars (\ ; =) in every string field
        var descriptor = new IntentDescriptor(
            action: "pay;v=1",
            issuer: "wallet\\service",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "acct=99;main",
            amount: 1m,
            currency: "EUR",
            issuedAt: IntentDescriptorFactory.DefaultBaseTime,
            expiresAt: IntentDescriptorFactory.DefaultBaseTime.AddMinutes(5),
            nonce: "n=1;x\\y");

        // Act
        var header = _serializer.Serialize(descriptor);

        // Assert — each special character is preceded by a backslash
        Assert.Contains(@"action=pay\;v\=1;", header);
        Assert.Contains(@"issuer=wallet\\service;", header);
        Assert.Contains(@"beneficiary=acct\=99\;main;", header);
        Assert.Contains(@"nonce=n\=1\;x\\y", header);
    }
}
