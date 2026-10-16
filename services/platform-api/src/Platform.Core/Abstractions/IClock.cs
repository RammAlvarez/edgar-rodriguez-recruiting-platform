namespace Platform.Core.Abstractions;

/// <summary>The only source of "now". Business dates are derived from it in a payroll group's zone, never from UTC.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Truncated to whole microseconds, the precision of a Postgres <c>timestamptz</c>: a stamp returned right after it is set
/// equals the one read back later (Linux ticks are 100 ns; macOS ticks are already microseconds, which hid it locally).
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        }
    }
}

public static class ClockExtensions
{
    public const string DefaultTimeZone = "America/Monterrey";

    /// <summary>The calendar date "today" in the given IANA zone.</summary>
    public static DateOnly Today(this IClock clock, string timeZone = DefaultTimeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
}
