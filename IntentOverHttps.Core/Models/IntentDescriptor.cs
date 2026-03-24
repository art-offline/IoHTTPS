using System.Globalization;

namespace IntentOverHttps.Core.Models;

public sealed record class IntentDescriptor
{
    public IntentDescriptor(
        string action,
        string issuer,
        Uri targetOrigin,
        string beneficiary,
        decimal amount,
        string currency,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string nonce)
    {
        Action = NormalizeRequired(action, nameof(action));
        Issuer = NormalizeRequired(issuer, nameof(issuer));
        TargetOrigin = NormalizeOrigin(targetOrigin, nameof(targetOrigin));
        Beneficiary = NormalizeRequired(beneficiary, nameof(beneficiary));
        Amount = amount > 0m
            ? amount
            : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        Currency = NormalizeCurrency(currency, nameof(currency));
        IssuedAt = issuedAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
        Nonce = NormalizeRequired(nonce, nameof(nonce));

        if (ExpiresAt <= IssuedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), expiresAt, "ExpiresAt must be later than IssuedAt.");
        }
    }

    public string Action { get; }

    public string Issuer { get; }

    public Uri TargetOrigin { get; }

    public string Beneficiary { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public string Nonce { get; }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
        }

        return value.Trim();
    }

    private static string NormalizeCurrency(string currency, string paramName)
    {
        var normalized = NormalizeRequired(currency, paramName);

        if (normalized.Length != 3 || normalized.Any(static c => !char.IsAsciiLetter(c)))
        {
            throw new ArgumentException("Currency must be a 3-letter alphabetic code.", paramName);
        }

        return normalized.ToUpperInvariant();
    }

    private static Uri NormalizeOrigin(Uri targetOrigin, string paramName)
    {
        ArgumentNullException.ThrowIfNull(targetOrigin, paramName);

        if (!targetOrigin.IsAbsoluteUri)
        {
            throw new ArgumentException("Target origin must be an absolute URI.", paramName);
        }

        if (!string.IsNullOrEmpty(targetOrigin.Query) || !string.IsNullOrEmpty(targetOrigin.Fragment))
        {
            throw new ArgumentException("Target origin must not include a query string or fragment.", paramName);
        }

        var path = targetOrigin.AbsolutePath;
        if (!string.IsNullOrEmpty(path) && path != "/")
        {
            throw new ArgumentException("Target origin must not include a path.", paramName);
        }

        return new Uri(targetOrigin.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
    }
}

