using IntentOverHttps.Core.Models;

namespace IntentOverHttps.Tests.Core.Fakes;

/// <summary>
/// Builds <see cref="IntentDescriptor"/> instances with sensible defaults,
/// so individual tests only need to specify the fields they care about.
/// </summary>
internal static class IntentDescriptorFactory
{
    /// <summary>
    /// A fixed reference point in time used as the default <c>issuedAt</c>.
    /// ExpiresAt defaults to this value plus five minutes.
    /// </summary>
    internal static readonly DateTimeOffset DefaultBaseTime =
        new(2026, 3, 24, 12, 0, 0, TimeSpan.Zero);

    internal static IntentDescriptor CreateValid(
        string action = "pay",
        string issuer = "test-issuer",
        Uri? targetOrigin = null,
        string beneficiary = "test-beneficiary",
        decimal amount = 10.00m,
        string currency = "EUR",
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null,
        string nonce = "test-nonce-001")
    {
        var issued = issuedAt ?? DefaultBaseTime;

        return new IntentDescriptor(
            action,
            issuer,
            targetOrigin ?? new Uri("https://merchant.example"),
            beneficiary,
            amount,
            currency,
            issued,
            expiresAt ?? issued.AddMinutes(5),
            nonce);
    }
}

