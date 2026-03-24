using IntentOverHttps.Core.Models;
using IntentOverHttps.Tests.Core.Fakes;

namespace IntentOverHttps.Tests.Core.Models;

public sealed class IntentDescriptorTests
{
    private static readonly DateTimeOffset Now = IntentDescriptorFactory.DefaultBaseTime;

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldCreateDescriptor_WhenAllArgumentsAreValid()
    {
        // Act
        var descriptor = IntentDescriptorFactory.CreateValid();

        // Assert
        Assert.Equal("pay", descriptor.Action);
        Assert.Equal("test-issuer", descriptor.Issuer);
        Assert.Equal(new Uri("https://merchant.example"), descriptor.TargetOrigin);
        Assert.Equal("test-beneficiary", descriptor.Beneficiary);
        Assert.Equal(10.00m, descriptor.Amount);
        Assert.Equal("EUR", descriptor.Currency);
        Assert.Equal("test-nonce-001", descriptor.Nonce);
        Assert.Equal(Now, descriptor.IssuedAt);
        Assert.Equal(Now.AddMinutes(5), descriptor.ExpiresAt);
    }

    [Fact]
    public void Constructor_ShouldNormalizeCurrencyToUpperCase_WhenInputIsLowerCase()
    {
        // Act
        var descriptor = IntentDescriptorFactory.CreateValid(currency: "eur");

        // Assert
        Assert.Equal("EUR", descriptor.Currency);
    }

    [Fact]
    public void Constructor_ShouldNormalizeCurrencyToUpperCase_WhenInputIsMixedCase()
    {
        // Act
        var descriptor = IntentDescriptorFactory.CreateValid(currency: "uSd");

        // Assert
        Assert.Equal("USD", descriptor.Currency);
    }

    [Fact]
    public void Constructor_ShouldNormalizeTimestampsToUtc_WhenInputHasNonZeroOffset()
    {
        // Arrange
        var issued = new DateTimeOffset(2026, 3, 24, 14, 0, 0, TimeSpan.FromHours(2)); // +02:00 = 12:00 UTC
        var expires = issued.AddMinutes(5);

        // Act
        var descriptor = IntentDescriptorFactory.CreateValid(issuedAt: issued, expiresAt: expires);

        // Assert
        Assert.Equal(TimeSpan.Zero, descriptor.IssuedAt.Offset);
        Assert.Equal(TimeSpan.Zero, descriptor.ExpiresAt.Offset);
        Assert.Equal(issued.ToUniversalTime(), descriptor.IssuedAt);
        Assert.Equal(expires.ToUniversalTime(), descriptor.ExpiresAt);
    }

    [Fact]
    public void Constructor_ShouldTrimLeadingAndTrailingWhitespace_FromAllStringFields()
    {
        // Act
        var descriptor = new IntentDescriptor(
            action: "  pay  ",
            issuer: "  issuer  ",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "  beneficiary  ",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "  nonce  ");

        // Assert
        Assert.Equal("pay", descriptor.Action);
        Assert.Equal("issuer", descriptor.Issuer);
        Assert.Equal("beneficiary", descriptor.Beneficiary);
        Assert.Equal("nonce", descriptor.Nonce);
    }

    [Fact]
    public void Constructor_ShouldNormalizeTargetOrigin_ByStrippingTrailingSlash()
    {
        // Act
        var descriptor = IntentDescriptorFactory.CreateValid(
            targetOrigin: new Uri("https://merchant.example/"));

        // Assert — only scheme + authority are preserved; System.Uri always
        // serializes root URIs with a trailing slash, so we compare the authority part.
        Assert.Equal(
            "https://merchant.example",
            descriptor.TargetOrigin.GetLeftPart(UriPartial.Authority));
        Assert.True(
            string.IsNullOrEmpty(descriptor.TargetOrigin.AbsolutePath) ||
            descriptor.TargetOrigin.AbsolutePath == "/");
    }

    // ── Required string fields — null / empty / whitespace ───────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenActionIsNullOrWhiteSpace(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: value!,
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenIssuerIsNullOrWhiteSpace(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: value!,
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenBeneficiaryIsNullOrWhiteSpace(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: value!,
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenNonceIsNullOrWhiteSpace(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: value!));
    }

    // ── Amount ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ShouldThrow_WhenAmountIsNotPositive(int rawAmount)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: rawAmount,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    // ── Currency ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("EU")]    // too short
    [InlineData("EURO")]  // too long
    [InlineData("1EU")]   // contains a digit
    [InlineData("E R")]   // contains a space
    public void Constructor_ShouldThrow_WhenCurrencyFormatIsInvalid(string currency)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: currency,
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    // ── TargetOrigin ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldThrow_WhenTargetOriginIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: null!,
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTargetOriginIsRelative()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("/relative/path", UriKind.Relative),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTargetOriginIncludesAPath()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example/shop"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTargetOriginIncludesAQueryString()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example?ref=1"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddMinutes(1),
            nonce: "nonce"));
    }

    // ── Temporal range ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldThrow_WhenExpiresAtEqualsIssuedAt()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now, // same instant → invalid
            nonce: "nonce"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenExpiresAtIsBeforeIssuedAt()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntentDescriptor(
            action: "pay",
            issuer: "issuer",
            targetOrigin: new Uri("https://merchant.example"),
            beneficiary: "beneficiary",
            amount: 1m,
            currency: "EUR",
            issuedAt: Now,
            expiresAt: Now.AddSeconds(-1), // in the past relative to issuedAt
            nonce: "nonce"));
    }
}

