namespace IntentOverHttps.Tests.Core.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> that returns a fixed point in time,
/// useful for deterministic temporal tests.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    internal FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    /// <summary>Shifts the current time forward by <paramref name="duration"/>.</summary>
    internal void AdvanceBy(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

