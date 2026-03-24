using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Serialization;
using IntentOverHttps.Core.Validation;

namespace IntentOverHttps.Tests.Core.Serialization;

public sealed class IntentHeaderParserTests
{
    private readonly IntentHeaderSerializer _serializer = new();
    private readonly IntentHeaderParser _parser = new();

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldRoundTripSerializedDescriptor()
    {
        // Arrange
        var descriptor = new IntentDescriptor(
            action: "capture",
            issuer: "wallet-service",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "merchant=123;settlement\\desk",
            amount: 87.5m,
            currency: "usd",
            issuedAt: new DateTimeOffset(2026, 03, 24, 08, 00, 00, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 03, 24, 08, 05, 00, TimeSpan.Zero),
            nonce: "nonce=001;A\\B");

        var header = _serializer.Serialize(descriptor);

        // Act
        var result = _parser.Parse(header, out var parsedDescriptor);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.NotNull(parsedDescriptor);
        Assert.Equal(descriptor, parsedDescriptor);
    }

    // ── Missing fields ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldReportAllNineRequiredFields_WhenHeaderIsWhitespaceOnly()
    {
        // Act
        var result = _parser.Parse("   ", out var descriptor);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(descriptor);
        Assert.Equal(9, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(IntentErrorCode.MissingField, error.Code));
    }

    [Fact]
    public void Parse_ShouldReportAllNineRequiredFields_WhenHeaderIsNull()
    {
        // Act
        var result = _parser.Parse(null, out var descriptor);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(descriptor);
        Assert.Equal(9, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(IntentErrorCode.MissingField, error.Code));
    }

    [Fact]
    public void Parse_ShouldReportEachMissingField_ByName()
    {
        // Act — only action is provided
        const string header = "action=pay";
        var result = _parser.Parse(header, out _);

        // Assert — the other 8 required fields are each reported missing
        var missingFields = result.Errors
            .Where(e => e.Code == IntentErrorCode.MissingField)
            .Select(e => e.Field)
            .ToHashSet();

        Assert.Contains("issuer", missingFields);
        Assert.Contains("targetOrigin", missingFields);
        Assert.Contains("beneficiary", missingFields);
        Assert.Contains("amount", missingFields);
        Assert.Contains("currency", missingFields);
        Assert.Contains("issuedAt", missingFields);
        Assert.Contains("expiresAt", missingFields);
        Assert.Contains("nonce", missingFields);
    }

    // ── Multi-error accumulation ──────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldAccumulateAllErrors_WithoutStoppingAtTheFirstFailure()
    {
        // Arrange — every field is either missing or deliberately malformed
        const string header =
            "action=pay;issuer= ;targetOrigin=not-a-uri;amount=-1;currency=EURO;issuedAt=not-a-date;expiresAt=still-not-a-date;nonce=";

        // Act
        var result = _parser.Parse(header, out var descriptor);

        // Assert — all errors are gathered in one pass
        Assert.False(result.IsValid);
        Assert.Null(descriptor);
        Assert.Collection(
            result.Errors.OrderBy(e => e.Field, StringComparer.Ordinal).ThenBy(e => e.Code),
            e => AssertError(e, IntentErrorCode.InvalidAmount, "amount"),
            e => AssertError(e, IntentErrorCode.MissingField, "beneficiary"),
            e => AssertError(e, IntentErrorCode.InvalidCurrency, "currency"),
            e => AssertError(e, IntentErrorCode.InvalidExpiresAt, "expiresAt"),
            e => AssertError(e, IntentErrorCode.InvalidIssuedAt, "issuedAt"),
            e => AssertError(e, IntentErrorCode.MissingField, "issuer"),
            e => AssertError(e, IntentErrorCode.MissingField, "nonce"),
            e => AssertError(e, IntentErrorCode.InvalidTargetOrigin, "targetOrigin"));
    }

    // ── Structural errors ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldReportDuplicateAndUnknownFields()
    {
        // Arrange
        const string header =
            "action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EUR;" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1;" +
            "extra=value;issuer=issuer-b";

        // Act
        var result = _parser.Parse(header, out _);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.UnknownField && e.Field == "extra");
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.DuplicateField && e.Field == "issuer");
    }

    [Fact]
    public void Parse_ShouldRejectHeader_WhenItEndsWithAnIncompleteEscapeSequence()
    {
        // Arrange — trailing backslash is an unterminated escape
        const string header =
            "action=pay;issuer=wallet;targetOrigin=https://merchant.example;beneficiary=b;amount=1;currency=EUR;" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n\\";

        // Act
        var result = _parser.Parse(header, out _);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == IntentErrorCode.MalformedField);
    }

    // ── Temporal range ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldRejectDescriptor_WhenExpiresAtIsNotLaterThanIssuedAt()
    {
        // Arrange — expiresAt == issuedAt
        const string header =
            "action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;currency=EUR;" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:00:00.0000000+00:00;nonce=n-1";

        // Act
        var result = _parser.Parse(header, out var descriptor);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(descriptor);
        Assert.Contains(result.Errors, e =>
            e.Code == IntentErrorCode.TemporalRangeInvalid && e.Field == "expiresAt");
    }

    // ── Field-level validation ────────────────────────────────────────────────

    [Fact]
    public void Parse_ShouldRejectDescriptor_WhenTargetOriginIncludesAPath()
    {
        // Arrange
        const string header =
            "action=pay;issuer=issuer-a;targetOrigin=https://merchant.example/shop;beneficiary=shop;amount=1.25;currency=EUR;" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1";

        // Act
        var result = _parser.Parse(header, out var descriptor);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(descriptor);
        Assert.Contains(result.Errors, e =>
            e.Code == IntentErrorCode.InvalidTargetOrigin && e.Field == "targetOrigin");
    }

    [Theory]
    [InlineData("amount=0")]
    [InlineData("amount=-1")]
    [InlineData("amount=-0.01")]
    public void Parse_ShouldRejectDescriptor_WhenAmountIsNotPositive(string amountSegment)
    {
        // Arrange
        var header =
            $"action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;{amountSegment};currency=EUR;" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1";

        // Act
        var result = _parser.Parse(header, out _);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Code == IntentErrorCode.InvalidAmount && e.Field == "amount");
    }

    [Theory]
    [InlineData("currency=EU")]    // too short
    [InlineData("currency=EURO")]  // too long
    [InlineData("currency=1EU")]   // contains a digit
    public void Parse_ShouldRejectDescriptor_WhenCurrencyCodeIsInvalid(string currencySegment)
    {
        // Arrange
        var header =
            $"action=pay;issuer=issuer-a;targetOrigin=https://merchant.example;beneficiary=shop;amount=1.25;{currencySegment};" +
            "issuedAt=2026-03-24T10:00:00.0000000+00:00;expiresAt=2026-03-24T10:05:00.0000000+00:00;nonce=n-1";

        // Act
        var result = _parser.Parse(header, out _);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Code == IntentErrorCode.InvalidCurrency && e.Field == "currency");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AssertError(IntentValidationError error, IntentErrorCode expectedCode, string? expectedField)
    {
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedField, error.Field);
    }
}
