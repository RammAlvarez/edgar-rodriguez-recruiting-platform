namespace Platform.Payroll.Engine;

/// <summary>
/// The pre-payroll line calculation of SPEC §5.5. Pure, C# <see cref="decimal"/>, nothing is rounded: each item is
/// calculated exactly and stored cut to 6 decimals; only the worker's net — the last calculation — is truncated to
/// cents (PLAN D42). Order: earnings → deductions → net at 6 decimals → truncate.
/// </summary>
public static class PrepayrollCalculator
{
    public static PrepayrollResult Calculate(PrepayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var rules = input.Rules;
        var rates = input.Rates;
        var att = input.Attendance;
        var items = new List<LineItem>();

        void Add(PayConcept concept, decimal quantity, decimal unitValue, decimal amount, ItemSource source,
            Guid? incidentId = null, PayConcept? exportAs = null)
        {
            amount = TruncateToStored(amount);
            if (amount == 0m)
            {
                return;
            }
            var info = concept.Info();
            items.Add(new LineItem(concept, info.Kind, (exportAs ?? concept).Info().ExportColumn,
                quantity, unitValue, amount, source, incidentId));
        }

        var sd = rates.DailySalaryPocket;
        var hourly = rates.HourlyRate;
        var days = att.DaysWorked;

        Add(PayConcept.Salary, days, sd, sd * days, ItemSource.Attendance);
        Add(PayConcept.SeventhDay, days, sd, sd * days / rules.SeventhDayDivisor, ItemSource.Attendance);
        if (att.WorkedSunday)
        {
            Add(PayConcept.SundayPremium, 1m, sd, sd * rules.SundayPremiumPct, ItemSource.Attendance);
        }
        Add(PayConcept.PartialHours, att.PartialHours, hourly, hourly * att.PartialHours, ItemSource.Attendance);
        var holidayHours = GroupRules.HolidayFirstHalfHours * att.HolidaysEntitled;
        var holidayRate = hourly * GroupRules.HolidayFirstHalfMultiplier;
        Add(PayConcept.HolidayFirstHalf, holidayHours, holidayRate, holidayRate * holidayHours, ItemSource.Attendance);
        Add(PayConcept.Overtime, att.OvertimeHours, hourly, hourly * att.OvertimeHours, ItemSource.Attendance);
        var holidayWorkedRate = sd * rules.HolidayWorkedMultiplier;
        Add(PayConcept.HolidayWorked, att.HolidaysWorked, holidayWorkedRate, holidayWorkedRate * att.HolidaysWorked,
            ItemSource.Attendance);

        foreach (var incident in input.Incidents)
        {
            var multiplier = HourMultiplier(incident.Concept);
            if (multiplier is { } m)
            {
                Add(incident.Concept, incident.Quantity, hourly * m, hourly * m * incident.Quantity,
                    ItemSource.Incident, incident.IncidentId);
            }
            else if (incident.Concept == PayConcept.HolidayWorked)
            {
                Add(incident.Concept, incident.Quantity, holidayWorkedRate, holidayWorkedRate * incident.Quantity,
                    ItemSource.Incident, incident.IncidentId);
            }
            else if (incident.Concept == PayConcept.ManualAdjustment)
            {
                Add(incident.Concept, 1m, incident.Amount, incident.Amount, ItemSource.Manual, incident.IncidentId,
                    incident.AdjustedConcept);
            }
            else
            {
                Add(incident.Concept, 1m, incident.Amount, incident.Amount, ItemSource.Incident, incident.IncidentId);
            }
        }

        if (days > 0m)
        {
            foreach (var bonus in input.Bonuses)
            {
                var amount = bonus.Concept == PayConcept.BonusDaysWorked && bonus.Amount == 0m
                    ? rates.DaysWorkedBonus
                    : bonus.Amount;
                if (bonus.DaysBasis is { } basis)
                {
                    Add(bonus.Concept, days, amount, amount * days / basis, ItemSource.Position);
                }
                else
                {
                    Add(bonus.Concept, 1m, amount, amount, ItemSource.Position);
                }
            }
        }

        var ordered = items.OrderBy(i => i.Concept).ToList();
        var gross = ordered.Where(i => i.Kind == ConceptKind.Earning).Sum(i => i.Amount);
        var deductions = ordered.Where(i => i.Kind == ConceptKind.Deduction).Sum(i => i.Amount);
        var exact = gross - deductions;
        return new PrepayrollResult(ordered, gross, deductions, exact, TruncateToCents(exact));
    }

    /// <summary>Cuts to two decimals toward zero, never rounding: 1,234.5678 → 1,234.56 (PLAN D42).</summary>
    public static decimal TruncateToCents(decimal amount) => Math.Round(amount, 2, MidpointRounding.ToZero);

    /// <summary>Decimals every computed amount is stored with (PLAN D42).</summary>
    public const int StoredDecimals = 6;

    /// <summary>Cuts a computed amount to <see cref="StoredDecimals"/> toward zero: 71.4283333… → 71.428333 (PLAN D42).</summary>
    public static decimal TruncateToStored(decimal amount) =>
        Math.Round(amount, StoredDecimals, MidpointRounding.ToZero);

    private static decimal? HourMultiplier(PayConcept concept) => concept switch
    {
        PayConcept.PartialHours or PayConcept.Overtime => 1m,
        PayConcept.OvertimeDouble => 2m,
        PayConcept.OvertimeTriple => 3m,
        _ => null,
    };

    private static readonly HashSet<PayConcept> AttendanceOnly =
    [
        PayConcept.Salary, PayConcept.SeventhDay, PayConcept.SundayPremium, PayConcept.HolidayFirstHalf,
        PayConcept.BonusDaysWorked, PayConcept.BonusPunctuality, PayConcept.BonusProductivity,
    ];

    private static readonly HashSet<PayConcept> BonusConcepts =
        [PayConcept.BonusDaysWorked, PayConcept.BonusPunctuality, PayConcept.BonusProductivity];

    private static void Validate(PrepayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Rules);
        ArgumentNullException.ThrowIfNull(input.Rates);
        ArgumentNullException.ThrowIfNull(input.Attendance);
        ArgumentNullException.ThrowIfNull(input.Incidents);
        ArgumentNullException.ThrowIfNull(input.Bonuses);

        if (input.Rules.SeventhDayDivisor <= 0)
        {
            throw new ArgumentException("The seventh-day divisor must be positive.", nameof(input));
        }
        var a = input.Attendance;
        if (a.DaysWorked < 0m || a.PartialHours < 0m || a.OvertimeHours < 0m || a.HolidaysEntitled < 0
            || a.HolidaysWorked < 0m)
        {
            throw new ArgumentException("Attendance quantities cannot be negative.", nameof(input));
        }
        foreach (var incident in input.Incidents)
        {
            if (AttendanceOnly.Contains(incident.Concept))
            {
                throw new ArgumentException(
                    $"{incident.Concept.Info().Code} comes from attendance or the group, not from an incident.",
                    nameof(input));
            }
            if (incident.Concept == PayConcept.ManualAdjustment
                && incident.AdjustedConcept is null or PayConcept.ManualAdjustment)
            {
                throw new ArgumentException("A manual adjustment must name the concept it adjusts.", nameof(input));
            }
            if (incident.Concept != PayConcept.ManualAdjustment && (incident.Quantity < 0m || incident.Amount < 0m))
            {
                throw new ArgumentException("Incident quantities and amounts cannot be negative.", nameof(input));
            }
        }
        foreach (var bonus in input.Bonuses)
        {
            if (!BonusConcepts.Contains(bonus.Concept))
            {
                throw new ArgumentException($"{bonus.Concept.Info().Code} is not a bonus concept.", nameof(input));
            }
            if (bonus.DaysBasis is <= 0m)
            {
                throw new ArgumentException("A bonus days basis must be positive.", nameof(input));
            }
        }
    }
}
