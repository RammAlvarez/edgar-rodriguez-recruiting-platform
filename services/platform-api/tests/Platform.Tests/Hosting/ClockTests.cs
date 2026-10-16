using Platform.Core.Abstractions;

namespace Platform.Tests.Hosting;

/// <summary>"Now" has the precision Postgres stores, so a stamp returned on write equals the one read back (CI on Linux).</summary>
public sealed class ClockTests
{
    [Fact]
    public void Now_is_whole_microseconds()
    {
        for (var i = 0; i < 1000; i++)
            Assert.Equal(0, new SystemClock().UtcNow.Ticks % TimeSpan.TicksPerMicrosecond);
    }
}
