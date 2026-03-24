namespace IntentOverHttps.Core.Validation;

public sealed class IntentValidationResult
{
    private static readonly IntentValidationResult SuccessInstance = new(Array.Empty<IntentValidationError>());

    public IntentValidationResult(IEnumerable<IntentValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors.ToArray();
    }

    public static IntentValidationResult Success => SuccessInstance;

    public IReadOnlyList<IntentValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static IntentValidationResult Failure(params IntentValidationError[] errors) => new(errors);

    public static IntentValidationResult Failure(IEnumerable<IntentValidationError> errors) => new(errors);
}

