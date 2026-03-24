namespace IntentOverHttps.Core.Validation;

public enum IntentErrorCode
{
    Unknown = 0,
    MissingField,
    MalformedField,
    UnknownField,
    DuplicateField,
    InvalidAction,
    InvalidIssuer,
    InvalidTargetOrigin,
    InvalidBeneficiary,
    InvalidAmount,
    InvalidCurrency,
    InvalidIssuedAt,
    InvalidExpiresAt,
    InvalidNonce,
    TemporalRangeInvalid,
    Expired,
    NotYetValid,
    SignatureInvalid,
    KeyNotFound,
    ReplayDetected
}

