using System.Globalization;

namespace Platform.Payroll.Engine;

/// <summary>Period numbers (PLAN D47).</summary>
public static class PeriodNumbering
{
    /// <summary>ISO-8601 week of the period's end date: 11–17 sep 2026 → Semana 38.</summary>
    public static int WeeklyNumber(DateOnly endDate) => ISOWeek.GetWeekOfYear(endDate.ToDateTime(TimeOnly.MinValue));

    /// <summary>The ISO year that week belongs to: a period ending 1 jan 2027 is Semana 53 of 2026.</summary>
    public static int WeeklyYear(DateOnly endDate) => ISOWeek.GetYear(endDate.ToDateTime(TimeOnly.MinValue));

    /// <summary>1–24 by end date: the first half of a month ends on the 15th or earlier.</summary>
    public static int BiweeklyNumber(DateOnly endDate) => (endDate.Month - 1) * 2 + (endDate.Day <= 15 ? 1 : 2);
}
