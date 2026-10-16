namespace Platform.Core.Abstractions;

/// <summary>The only source of "now". Business dates are derived from it in a payroll group's zone, never from UTC.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class ClockExtensions
{
    public const string DefaultTimeZone = "America/Monterrey";

    /// <summary>The calendar date "today" in the given IANA zone.</summary>
    public static DateOnly Today(this IClock clock, string timeZone = DefaultTimeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
}
