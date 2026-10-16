using Platform.Core.Abstractions;

namespace Platform.Tests.Support;

/// <summary>
/// The API's «now» in tests: real time unless a test sets <see cref="Now"/> (lock moments, job transitions). Reset before
/// every test by <see cref="ApiTest"/>.
/// </summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset? Now { get; set; }

    public DateTimeOffset UtcNow => Now ?? new SystemClock().UtcNow;

    /// <summary>Sets «now» to <paramref name="local"/> wall-clock time in America/Monterrey.</summary>
    public void SetMonterrey(DateTime local)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ClockExtensions.DefaultTimeZone);
        Now = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
