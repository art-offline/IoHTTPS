using System.Globalization;
using System.Text;
using IntentOverHttps.Core.Models;
using IntentOverHttps.Core.Validation;

namespace IntentOverHttps.Core.Serialization;

public sealed class IntentHeaderParser
{
    private static readonly string[] RequiredFieldNames =
    [
        "action",
        "issuer",
        "targetOrigin",
        "beneficiary",
        "amount",
        "currency",
        "issuedAt",
        "expiresAt",
        "nonce"
    ];

    private static readonly HashSet<string> KnownFieldNames = new(RequiredFieldNames, StringComparer.Ordinal);

    public IntentValidationResult Parse(string? headerValue, out IntentDescriptor? descriptor)
    {
        descriptor = null;
        var errors = new List<IntentValidationError>();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            AddMissingFieldErrors(errors);
            return IntentValidationResult.Failure(errors);
        }

        var rawFields = ParseFields(headerValue, errors);

        var action = ParseRequiredTextField(rawFields, "action", IntentErrorCode.InvalidAction, errors);
        var issuer = ParseRequiredTextField(rawFields, "issuer", IntentErrorCode.InvalidIssuer, errors);
        var targetOrigin = ParseTargetOrigin(rawFields, errors);
        var beneficiary = ParseRequiredTextField(rawFields, "beneficiary", IntentErrorCode.InvalidBeneficiary, errors);
        var amount = ParseAmount(rawFields, errors);
        var currency = ParseCurrency(rawFields, errors);
        var issuedAt = ParseTimestamp(rawFields, "issuedAt", IntentErrorCode.InvalidIssuedAt, errors);
        var expiresAt = ParseTimestamp(rawFields, "expiresAt", IntentErrorCode.InvalidExpiresAt, errors);
        var nonce = ParseRequiredTextField(rawFields, "nonce", IntentErrorCode.InvalidNonce, errors);

        if (issuedAt.HasValue && expiresAt.HasValue && expiresAt.Value <= issuedAt.Value)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.TemporalRangeInvalid,
                "Field 'expiresAt' must be later than 'issuedAt'.",
                "expiresAt"));
        }

        if (errors.Count > 0)
        {
            return IntentValidationResult.Failure(errors);
        }

        descriptor = new IntentDescriptor(
            action!,
            issuer!,
            targetOrigin!,
            beneficiary!,
            amount!.Value,
            currency!,
            issuedAt!.Value,
            expiresAt!.Value,
            nonce!);

        return IntentValidationResult.Success;
    }

    private static Dictionary<string, string> ParseFields(string headerValue, List<IntentValidationError> errors)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var segment in SplitSegments(headerValue, errors))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.MalformedField,
                    "Header contains an empty field segment."));
                continue;
            }

            if (!TrySplitField(segment, out var rawName, out var rawValue))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.MalformedField,
                    $"Header segment '{segment}' is malformed."));
                continue;
            }

            if (rawName.Contains('\\', StringComparison.Ordinal))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.MalformedField,
                    $"Field name '{rawName}' contains invalid escape sequences."));
                continue;
            }

            var fieldName = rawName.Trim();
            if (fieldName.Length == 0)
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.MalformedField,
                    "Header contains a field with no name."));
                continue;
            }

            if (!TryUnescape(rawValue, out var value))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.MalformedField,
                    $"Field '{fieldName}' contains an incomplete escape sequence.",
                    fieldName));
                continue;
            }

            if (!KnownFieldNames.Contains(fieldName))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.UnknownField,
                    $"Field '{fieldName}' is not supported.",
                    fieldName));
                continue;
            }

            if (!fields.TryAdd(fieldName, value))
            {
                errors.Add(new IntentValidationError(
                    IntentErrorCode.DuplicateField,
                    $"Field '{fieldName}' is duplicated.",
                    fieldName));
            }
        }

        return fields;
    }

    private static IEnumerable<string> SplitSegments(string headerValue, List<IntentValidationError> errors)
    {
        var segments = new List<string>();
        var builder = new StringBuilder(headerValue.Length);
        var escaping = false;

        foreach (var character in headerValue)
        {
            if (escaping)
            {
                builder.Append(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                builder.Append(character);
                escaping = true;
                continue;
            }

            if (character == ';')
            {
                segments.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        if (escaping)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.MalformedField,
                "Header ends with an incomplete escape sequence."));
        }

        segments.Add(builder.ToString());
        return segments;
    }

    private static bool TrySplitField(string segment, out string name, out string value)
    {
        var escaping = false;

        for (var index = 0; index < segment.Length; index++)
        {
            var character = segment[index];

            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (character != '=')
            {
                continue;
            }

            name = segment[..index];
            value = segment[(index + 1)..];
            return true;
        }

        name = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool TryUnescape(string rawValue, out string value)
    {
        var builder = new StringBuilder(rawValue.Length);
        var escaping = false;

        foreach (var character in rawValue)
        {
            if (escaping)
            {
                builder.Append(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            builder.Append(character);
        }

        value = builder.ToString();
        return !escaping;
    }

    private static string? ParseRequiredTextField(
        IReadOnlyDictionary<string, string> fields,
        string fieldName,
        IntentErrorCode invalidCode,
        List<IntentValidationError> errors)
    {
        if (!fields.TryGetValue(fieldName, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.MissingField,
                $"Field '{fieldName}' is required.",
                fieldName));
            return null;
        }

        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            errors.Add(new IntentValidationError(
                invalidCode,
                $"Field '{fieldName}' is invalid.",
                fieldName));
            return null;
        }

        return value;
    }

    private static Uri? ParseTargetOrigin(IReadOnlyDictionary<string, string> fields, List<IntentValidationError> errors)
    {
        var rawValue = ParseRequiredTextField(fields, "targetOrigin", IntentErrorCode.InvalidTargetOrigin, errors);
        if (rawValue is null)
        {
            return null;
        }

        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var targetOrigin))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidTargetOrigin,
                "Field 'targetOrigin' must be a valid absolute URI.",
                "targetOrigin"));
            return null;
        }

        if (!string.IsNullOrEmpty(targetOrigin.Query) || !string.IsNullOrEmpty(targetOrigin.Fragment))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidTargetOrigin,
                "Field 'targetOrigin' must not include a query string or fragment.",
                "targetOrigin"));
            return null;
        }

        var path = targetOrigin.AbsolutePath;
        if (!string.IsNullOrEmpty(path) && path != "/")
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidTargetOrigin,
                "Field 'targetOrigin' must not include a path.",
                "targetOrigin"));
            return null;
        }

        return new Uri(targetOrigin.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
    }

    private static decimal? ParseAmount(IReadOnlyDictionary<string, string> fields, List<IntentValidationError> errors)
    {
        var rawValue = ParseRequiredTextField(fields, "amount", IntentErrorCode.InvalidAmount, errors);
        if (rawValue is null)
        {
            return null;
        }

        if (!decimal.TryParse(
                rawValue,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidAmount,
                "Field 'amount' must be a valid invariant decimal number.",
                "amount"));
            return null;
        }

        if (amount <= 0m)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidAmount,
                "Field 'amount' must be greater than zero.",
                "amount"));
            return null;
        }

        return amount;
    }

    private static string? ParseCurrency(IReadOnlyDictionary<string, string> fields, List<IntentValidationError> errors)
    {
        var rawValue = ParseRequiredTextField(fields, "currency", IntentErrorCode.InvalidCurrency, errors);
        if (rawValue is null)
        {
            return null;
        }

        if (rawValue.Length != 3 || rawValue.Any(static c => !char.IsAsciiLetter(c)))
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.InvalidCurrency,
                "Field 'currency' must be a 3-letter alphabetic code.",
                "currency"));
            return null;
        }

        return rawValue.ToUpperInvariant();
    }

    private static DateTimeOffset? ParseTimestamp(
        IReadOnlyDictionary<string, string> fields,
        string fieldName,
        IntentErrorCode invalidCode,
        List<IntentValidationError> errors)
    {
        var rawValue = ParseRequiredTextField(fields, fieldName, invalidCode, errors);
        if (rawValue is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParseExact(
                rawValue,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            errors.Add(new IntentValidationError(
                invalidCode,
                $"Field '{fieldName}' must use the round-trip ISO-8601 format.",
                fieldName));
            return null;
        }

        return timestamp.ToUniversalTime();
    }

    private static void AddMissingFieldErrors(List<IntentValidationError> errors)
    {
        foreach (var fieldName in RequiredFieldNames)
        {
            errors.Add(new IntentValidationError(
                IntentErrorCode.MissingField,
                $"Field '{fieldName}' is required.",
                fieldName));
        }
    }
}

