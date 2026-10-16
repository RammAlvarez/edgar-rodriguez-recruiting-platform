using FluentValidation;
using Platform.Core.Http;

namespace Platform.Payroll.Groups;

/// <summary>«Nuevo grupo» / edit in Clientes (nómina) → client detail. The client never changes after creation.</summary>
public sealed record PayrollGroupRequest(Guid ClientId, string Name, PeriodType PeriodType, short CutoffWeekday, decimal ShiftHours,
    TimeOnly AttendanceLockTime, short SeventhDayDivisor, decimal SundayPremiumPct, decimal HolidayWorkedMultiplier,
    string? ScheduleDescription, string? TimeZone, bool IsActive);

/// <summary>«Nuevo puesto» with its first rates, valid from <see cref="ValidFrom"/> (default today).</summary>
public sealed record PositionRequest(string Name, decimal DailySalaryPocket, decimal DailySalaryImss, decimal HourlyRate,
    decimal DaysWorkedBonus, DateOnly? ValidFrom);

public sealed record UpdatePositionRequest(string Name, bool IsActive);

/// <summary>«Cambiar tarifas»: new rates from <see cref="ValidFrom"/>; the previous ones end the day before (history kept).</summary>
public sealed record PositionRatesRequest(decimal DailySalaryPocket, decimal DailySalaryImss, decimal HourlyRate, decimal DaysWorkedBonus,
    DateOnly ValidFrom);

/// <summary>A configurable concept of the group: on/off and its amount (and days basis for «× días / base»).</summary>
public sealed record GroupConceptRequest(string Code, bool IsActive, decimal? Amount, decimal? DaysBasis);

public sealed record GroupConceptsRequest(IReadOnlyList<GroupConceptRequest> Concepts);

public sealed record PositionRateDto(Guid Id, decimal DailySalaryPocket, decimal DailySalaryImss, decimal HourlyRate, decimal DaysWorkedBonus,
    DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>A puesto with the rates in force today (<c>null</c> when they start in the future) and every rate row, newest first.</summary>
public sealed record PositionDto(Guid Id, string Name, bool IsActive, PositionRateDto? Current, IReadOnlyList<PositionRateDto> History);

/// <summary>
/// A configurable concept as the group pays it. <see cref="AmountFromPosition"/>: BONO DÍAS TRABAJADOS takes each puesto's
/// amount unless the group sets one.
/// </summary>
public sealed record GroupConceptDto(string Code, string Name, ConceptCalc Calc, bool IsActive, decimal? Amount, decimal? DaysBasis,
    bool AmountFromPosition);

public sealed record PayrollGroupListItemDto(Guid Id, Guid ClientId, string ClientName, string Name, PeriodType PeriodType,
    short CutoffWeekday, decimal ShiftHours, TimeOnly AttendanceLockTime, bool IsActive, int Positions, int ActiveEmployees);

public sealed record PayrollGroupDetailDto(Guid Id, Guid ClientId, string ClientName, string Name, PeriodType PeriodType,
    short CutoffWeekday, decimal ShiftHours, TimeOnly AttendanceLockTime, short SeventhDayDivisor, decimal SundayPremiumPct,
    decimal HolidayWorkedMultiplier, string? ScheduleDescription, string TimeZone, bool IsActive, IReadOnlyList<PositionDto> Positions,
    IReadOnlyList<GroupConceptDto> Concepts);

internal static class RateRules
{
    public const string Money = "Importe en pesos con hasta 2 decimales.";
    public const string Hourly = "Importe en pesos con hasta 4 decimales.";

    public static bool IsMoney(decimal v) => v >= 0 && v < 1_000_000 && decimal.Round(v, 2) == v;

    public static bool IsHourly(decimal v) => v >= 0 && v < 100_000 && decimal.Round(v, 4) == v;

    public static void Rates<T>(AbstractValidator<T> v, Func<T, decimal> pocket, Func<T, decimal> imss, Func<T, decimal> hourly,
        Func<T, decimal> bonus)
    {
        v.RuleFor(r => pocket(r)).Must(IsMoney).WithMessage(Money).OverridePropertyName("dailySalaryPocket");
        v.RuleFor(r => imss(r)).Must(IsMoney).WithMessage(Money).OverridePropertyName("dailySalaryImss");
        v.RuleFor(r => hourly(r)).Must(IsHourly).WithMessage(Hourly).OverridePropertyName("hourlyRate");
        v.RuleFor(r => bonus(r)).Must(IsMoney).WithMessage(Money).OverridePropertyName("daysWorkedBonus");
    }
}

internal sealed class PayrollGroupRequestValidator : AbstractValidator<PayrollGroupRequest>
{
    public PayrollGroupRequestValidator()
    {
        RuleFor(r => r.Name).Must(n => n.Clean() is not null).WithMessage("Escribe el nombre del grupo.");
        RuleFor(r => r.Name).OptionalText(60);
        RuleFor(r => r.CutoffWeekday).InclusiveBetween((short)0, (short)6).WithMessage("Elige el día de corte.");
        RuleFor(r => r.ShiftHours).Must(h => h > 0 && h <= 24 && decimal.Round(h, 2) == h).WithMessage("Entre 1 y 24 horas.");
        RuleFor(r => r.SeventhDayDivisor).InclusiveBetween((short)1, (short)7).WithMessage("Entre 1 y 7.");
        RuleFor(r => r.SundayPremiumPct).InclusiveBetween(0m, 1m).WithMessage("Entre 0 y 1 (0.25 = 25 %).");
        RuleFor(r => r.HolidayWorkedMultiplier).InclusiveBetween(1m, 5m).WithMessage("Entre 1 y 5.");
        RuleFor(r => r.ScheduleDescription).OptionalText(120);
        RuleFor(r => r.TimeZone).Must(z => z is null || TimeZoneInfo.TryFindSystemTimeZoneById(z, out _))
            .WithMessage("Zona horaria desconocida (ejemplo: America/Monterrey).");
    }
}

internal sealed class PositionRequestValidator : AbstractValidator<PositionRequest>
{
    public PositionRequestValidator()
    {
        RuleFor(r => r.Name).Must(n => n.Clean() is not null).WithMessage("Escribe el puesto.");
        RuleFor(r => r.Name).OptionalText(60);
        RateRules.Rates(this, r => r.DailySalaryPocket, r => r.DailySalaryImss, r => r.HourlyRate, r => r.DaysWorkedBonus);
    }
}

internal sealed class UpdatePositionRequestValidator : AbstractValidator<UpdatePositionRequest>
{
    public UpdatePositionRequestValidator()
    {
        RuleFor(r => r.Name).Must(n => n.Clean() is not null).WithMessage("Escribe el puesto.");
        RuleFor(r => r.Name).OptionalText(60);
    }
}

internal sealed class PositionRatesRequestValidator : AbstractValidator<PositionRatesRequest>
{
    public PositionRatesRequestValidator() =>
        RateRules.Rates(this, r => r.DailySalaryPocket, r => r.DailySalaryImss, r => r.HourlyRate, r => r.DaysWorkedBonus);
}

internal sealed class GroupConceptsRequestValidator : AbstractValidator<GroupConceptsRequest>
{
    public GroupConceptsRequestValidator() =>
        RuleForEach(r => r.Concepts).ChildRules(c =>
        {
            c.RuleFor(x => x.Amount).Must(a => a is not { } v || RateRules.IsMoney(v)).WithMessage(RateRules.Money);
            c.RuleFor(x => x.DaysBasis).Must(d => d is not { } v || (v > 0 && v <= 31 && decimal.Round(v, 2) == v))
                .WithMessage("Entre 1 y 31 días.");
        });
}
