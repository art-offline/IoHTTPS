namespace IntentOverHttps.Core.Validation;

public sealed record class IntentValidationError(IntentErrorCode Code, string Message, string? Field = null);

