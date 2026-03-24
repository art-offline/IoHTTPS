using IntentOverHttps.Core.Abstractions;

namespace IntentOverHttps.Core.Verification;

public sealed class IntentVerificationOptions
{
    public IntentVerificationOptions(
        IKeyResolver? keyResolver = null,
        IReplayProtectionStore? replayProtectionStore = null,
        TimeProvider? timeProvider = null,
        TimeSpan? clockSkew = null,
        Uri? expectedTargetOrigin = null)
    {
        var effectiveClockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
        if (effectiveClockSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(clockSkew), effectiveClockSkew, "Clock skew must be greater than or equal to zero.");
        }

        KeyResolver = keyResolver;
        ReplayProtectionStore = replayProtectionStore;
        TimeProvider = timeProvider ?? TimeProvider.System;
        ClockSkew = effectiveClockSkew;
        ExpectedTargetOrigin = expectedTargetOrigin is null
            ? null
            : NormalizeOrigin(expectedTargetOrigin, nameof(expectedTargetOrigin));
    }

    public IKeyResolver? KeyResolver { get; }

    public IReplayProtectionStore? ReplayProtectionStore { get; }

    public TimeProvider TimeProvider { get; }

    public TimeSpan ClockSkew { get; }

    public Uri? ExpectedTargetOrigin { get; }

    private static Uri NormalizeOrigin(Uri value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        if (!value.IsAbsoluteUri)
        {
            throw new ArgumentException("Expected target origin must be an absolute URI.", paramName);
        }

        if (!string.IsNullOrEmpty(value.Query) || !string.IsNullOrEmpty(value.Fragment))
        {
            throw new ArgumentException("Expected target origin must not include a query string or fragment.", paramName);
        }

        var path = value.AbsolutePath;
        if (!string.IsNullOrEmpty(path) && path != "/")
        {
            throw new ArgumentException("Expected target origin must not include a path.", paramName);
        }

        return new Uri(value.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
    }
}

