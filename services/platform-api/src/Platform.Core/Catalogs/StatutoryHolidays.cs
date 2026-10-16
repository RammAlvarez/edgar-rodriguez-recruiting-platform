namespace Platform.Core.Catalogs;

public sealed record StatutoryHoliday(DateOnly Date, string NameEs);

/// <summary>
/// Días de descanso obligatorio of Ley Federal del Trabajo art. 74 for a year. Fraction VII (1 de octubre every six
/// years, transmisión del Poder Ejecutivo Federal) is generated from 2024 on; fraction IX (election days) depends on
/// each electoral calendar and is added by hand in Catálogos → Días festivos.
/// </summary>
public static class StatutoryHolidays
{
    public static IReadOnlyList<StatutoryHoliday> For(int year)
    {
        var list = new List<StatutoryHoliday>
        {
            new(new DateOnly(year, 1, 1), "Año nuevo"),
            new(NthMonday(year, 2, 1), "Día de la Constitución"),
            new(NthMonday(year, 3, 3), "Natalicio de Benito Juárez"),
            new(new DateOnly(year, 5, 1), "Día del Trabajo"),
            new(new DateOnly(year, 9, 16), "Día de la Independencia"),
            new(NthMonday(year, 11, 3), "Día de la Revolución"),
            new(new DateOnly(year, 12, 25), "Navidad"),
        };
        if (year >= 2024 && (year - 2024) % 6 == 0)
            list.Add(new(new DateOnly(year, 10, 1), "Transmisión del Poder Ejecutivo Federal"));
        return list.OrderBy(h => h.Date).ToList();
    }

    private static DateOnly NthMonday(int year, int month, int n)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)DayOfWeek.Monday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (n - 1));
    }
}
