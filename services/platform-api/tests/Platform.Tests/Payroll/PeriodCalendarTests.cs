using Platform.Payroll.Groups;
using Platform.Payroll.Periods;

namespace Platform.Tests.Payroll;

/// <summary>Period boundaries and numbers (PLAN D47, D83).</summary>
public sealed class PeriodCalendarTests
{
    private static readonly PayrollGroup Ecovia = new() { Name = "VALIDADORES 12 HRS", CutoffWeekday = 4 }; // jueves
    private static readonly PayrollGroup Heb = new() { Name = "LIMPIEZA OFICINAS", PeriodType = PeriodType.Biweekly, CutoffWeekday = 5 };

    [Theory]
    [InlineData("2026-09-11", "2026-09-11", "2026-09-17", 38)] // viernes, first day
    [InlineData("2026-09-14", "2026-09-11", "2026-09-17", 38)]
    [InlineData("2026-09-17", "2026-09-11", "2026-09-17", 38)] // jueves, the cut-off itself
    [InlineData("2026-09-18", "2026-09-18", "2026-09-24", 39)]
    public void A_weekly_period_runs_to_the_cutoff_weekday_and_is_numbered_by_its_end(string date, string start, string end, int number)
    {
        var p = PeriodCalendar.Containing(Ecovia, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal((start, end, number, 2026), (p.Start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            p.End.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), p.Number, p.Year));
    }

    [Fact]
    public void A_week_ending_in_january_belongs_to_the_iso_year_of_its_end()
    {
        var p = PeriodCalendar.Containing(Ecovia, new DateOnly(2026, 12, 30));
        Assert.Equal((new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 31), 53, 2026), (p.Start, p.End, p.Number, p.Year));
    }

    [Theory]
    [InlineData(2026, 10, 3, 1, 15, 19)]
    [InlineData(2026, 10, 16, 16, 31, 20)]
    [InlineData(2027, 2, 20, 16, 28, 4)]
    public void A_biweekly_period_is_a_calendar_half(int y, int m, int d, int startDay, int endDay, int number)
    {
        var p = PeriodCalendar.Containing(Heb, new DateOnly(y, m, d));
        Assert.Equal((new DateOnly(y, m, startDay), new DateOnly(y, m, endDay), number), (p.Start, p.End, p.Number));
    }
}
