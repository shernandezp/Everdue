using Everdue.Server.Application.Abstractions;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// The reason <see cref="IClock"/> exists. Fourteen days of downtime is a property change here,
/// not a fourteen-day test.
/// </summary>
public sealed class TestClock(DateTimeOffset? start = null) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start ?? DateTimeOffset.Parse("2026-07-27T12:00:00Z");

    public TestClock Advance(TimeSpan by)
    {
        UtcNow += by;
        return this;
    }

    public TestClock AdvanceDays(double days) => Advance(TimeSpan.FromDays(days));

    public TestClock Set(string utcInstant)
    {
        UtcNow = DateTimeOffset.Parse(utcInstant);
        return this;
    }

    public TestClock Set(DateTimeOffset instant)
    {
        UtcNow = instant;
        return this;
    }
}
