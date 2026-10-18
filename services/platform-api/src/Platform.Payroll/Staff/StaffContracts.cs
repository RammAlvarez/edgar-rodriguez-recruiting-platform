using FluentValidation;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

public sealed record EmployeeRatesDto(decimal DailySalaryPocket, decimal DailySalaryImss, decimal HourlyRate, decimal DaysWorkedBonus);

public sealed record StatusPeriodDto(Guid Id, EmployeeStatus Status, DateOnly StartDate, DateOnly? EndDate, string? Reason, string? Reference);

public sealed record CardRequestDto(Guid Id, Guid EmployeeId, RegistrationBank Bank, CardRequestStatus Status, DateTimeOffset RequestedAt);

/// <summary>A row of «Tarjetas por solicitar» (Prenómina side panel): who, where and which bank.</summary>
public sealed record CardQueueItemDto(Guid Id, Guid EmployeeId, string FullName, string ClientName, string GroupName, RegistrationBank Bank,
    CardRequestStatus Status, DateTimeOffset RequestedAt);

/// <summary>
/// A row of Personal (SPEC §5.1). <see cref="Status"/> is today's (America/Monterrey); <see cref="Period"/> is the period
/// behind it, or the next scheduled one. <see cref="HasAccount"/> = the person has a current primary account.
/// </summary>
public sealed record EmployeeListItemDto(Guid Id, Guid PersonId, string FullName, Guid ClientId, string ClientName, Guid PayrollGroupId,
    string GroupName, Guid PositionId, string PositionName, string Shift, DateOnly HiredAt, DateOnly? TerminatedAt, EmployeeStatus Status,
    StatusPeriodDto? Period, bool HasAccount, CardRequestDto? CardRequest, decimal DocumentsCompleteness);

/// <summary>
/// The employee detail (tabs Datos / Cuentas de pago / Documentos …). <see cref="Rates"/> are the rates the next period pays:
/// the puesto's current ones with the employee's overrides on top.
/// </summary>
public sealed record EmployeeDetailDto(Guid Id, Guid PersonId, string FullName, string? Curp, string? Rfc, string? Nss, string? MobilePhone,
    string? Email, bool IsForeign, Guid ClientId, string ClientName, Guid PayrollGroupId, string GroupName, Guid PositionId,
    string PositionName, string Shift, DateOnly HiredAt, DateOnly? TerminatedAt, EmployeeStatus Status, StatusPeriodDto? Period,
    StatusPeriodDto? NextPeriod, decimal? DailySalaryPocketOverride, decimal? DailySalaryImssOverride, decimal? HourlyRateOverride,
    decimal? DaysWorkedBonusOverride, EmployeeRatesDto? PositionRates, EmployeeRatesDto? Rates, string? Notes, Guid? CandidateId,
    Guid? DocumentTemplateId, bool HasAccount, CardRequestDto? CardRequest);

/// <summary>
/// «Alta de empleado», direct or «Convertir en empleado» (<see cref="CandidateId"/>). The worker's account can be typed at
/// alta (<see cref="BankAccount"/>, it becomes the principal); without it and without a current principal account the
/// employee is queued for a card at <see cref="CardBank"/> (SPEC §5.5, §5.7).
/// </summary>
public sealed record EmployeeRequest(Guid PersonId, Guid PayrollGroupId, Guid PositionId, string Shift, DateOnly? HiredAt,
    decimal? DailySalaryPocketOverride, decimal? DailySalaryImssOverride, decimal? HourlyRateOverride, decimal? DaysWorkedBonusOverride,
    string? Notes, RegistrationBank? CardBank, Guid? CandidateId, BankAccountRequest? BankAccount = null);

public sealed record UpdateEmployeeRequest(string Shift, decimal? DailySalaryPocketOverride, decimal? DailySalaryImssOverride,
    decimal? HourlyRateOverride, decimal? DaysWorkedBonusOverride, string? Notes);

/// <summary>«Cambio de cliente / puesto»: from now on (the change is kept in the audit trail, PLAN D82).</summary>
public sealed record AssignmentRequest(Guid PayrollGroupId, Guid PositionId, string Shift);

/// <summary>
/// «Cambiar estatus» (SPEC §5.1): Vacaciones, Incapacidad, Suspensión or Baja. When it covers days of a period already paid it is
/// refused with a notice (field <c>confirmPaidDays</c>) until <see cref="ConfirmPaidDays"/> says the user saw it (PLAN D95).
/// </summary>
public sealed record StatusPeriodRequest(EmployeeStatus Status, DateOnly StartDate, DateOnly? EndDate, string? Reason, string? Reference,
    bool ConfirmPaidDays = false);

/// <summary>The estimated end of an incapacidad (or any period) moved; the attendance days follow.</summary>
public sealed record UpdateStatusPeriodRequest(DateOnly EndDate, string? Reference, bool ConfirmPaidDays = false);

/// <summary>«Reingreso» after a baja: a new employment.</summary>
public sealed record RehireRequest(Guid PayrollGroupId, Guid PositionId, string Shift, DateOnly HiredAt, RegistrationBank? CardBank);

public sealed record CardRequestRequest(RegistrationBank Bank);

public sealed record PositionOptionDto(Guid Id, string Name, EmployeeRatesDto? Rates);

public sealed record SiteOptionDto(Guid Id, string Name);

/// <summary>A group for the alta and assignment pickers, with its puestos and the active sitios of its client (horarios).</summary>
public sealed record GroupOptionDto(Guid Id, Guid ClientId, string ClientName, string Name, IReadOnlyList<PositionOptionDto> Positions,
    IReadOnlyList<SiteOptionDto> Sites);

/// <summary>What «Convertir en empleado» pre-fills: the hire date and the bank the vacante pays with.</summary>
public sealed record ConversionDto(Guid CandidateId, Guid PersonId, string FullName, Guid ClientId, string OpeningName, DateOnly? HiredAt,
    RegistrationBank? SuggestedBank, bool HasAccount, Guid? EmployeeId);

public sealed record EmployeeOptionsDto(IReadOnlyList<GroupOptionDto> Groups, IReadOnlyList<string> Shifts,
    IReadOnlyList<string> TerminationReasons);

internal static class OverrideRules
{
    public static void Overrides<T>(AbstractValidator<T> v, Func<T, decimal?> pocket, Func<T, decimal?> imss, Func<T, decimal?> hourly,
        Func<T, decimal?> bonus)
    {
        v.RuleFor(r => pocket(r)).Must(x => x is not { } d || RateRules.IsMoney(d)).WithMessage(RateRules.Money)
            .OverridePropertyName("dailySalaryPocketOverride");
        v.RuleFor(r => imss(r)).Must(x => x is not { } d || RateRules.IsMoney(d)).WithMessage(RateRules.Money)
            .OverridePropertyName("dailySalaryImssOverride");
        v.RuleFor(r => hourly(r)).Must(x => x is not { } d || RateRules.IsHourly(d)).WithMessage(RateRules.Hourly)
            .OverridePropertyName("hourlyRateOverride");
        v.RuleFor(r => bonus(r)).Must(x => x is not { } d || RateRules.IsMoney(d)).WithMessage(RateRules.Money)
            .OverridePropertyName("daysWorkedBonusOverride");
    }
}

internal sealed class EmployeeRequestValidator : AbstractValidator<EmployeeRequest>
{
    public EmployeeRequestValidator()
    {
        RuleFor(r => r.Shift).Must(s => s.Clean() is not null).WithMessage("Escribe el turno.");
        RuleFor(r => r.Shift).OptionalText(30);
        RuleFor(r => r.Notes).OptionalText(500);
        OverrideRules.Overrides(this, r => r.DailySalaryPocketOverride, r => r.DailySalaryImssOverride, r => r.HourlyRateOverride,
            r => r.DaysWorkedBonusOverride);
        RuleFor(r => r.BankAccount!.AccountNumber).Must(n => AccountNumbers.Digits(n ?? "").Length > 0).WithMessage("Escribe el número.")
            .OverridePropertyName("bankAccount.accountNumber").When(r => r.BankAccount is not null);
        RuleFor(r => r.BankAccount!.HolderName).OptionalText(BankAccounts.HolderMax).OverridePropertyName("bankAccount.holderName")
            .When(r => r.BankAccount is not null);
    }
}

internal sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(r => r.Shift).Must(s => s.Clean() is not null).WithMessage("Escribe el turno.");
        RuleFor(r => r.Shift).OptionalText(30);
        RuleFor(r => r.Notes).OptionalText(500);
        OverrideRules.Overrides(this, r => r.DailySalaryPocketOverride, r => r.DailySalaryImssOverride, r => r.HourlyRateOverride,
            r => r.DaysWorkedBonusOverride);
    }
}

internal sealed class AssignmentRequestValidator : AbstractValidator<AssignmentRequest>
{
    public AssignmentRequestValidator()
    {
        RuleFor(r => r.Shift).Must(s => s.Clean() is not null).WithMessage("Escribe el turno.");
        RuleFor(r => r.Shift).OptionalText(30);
    }
}

internal sealed class RehireRequestValidator : AbstractValidator<RehireRequest>
{
    public RehireRequestValidator()
    {
        RuleFor(r => r.Shift).Must(s => s.Clean() is not null).WithMessage("Escribe el turno.");
        RuleFor(r => r.Shift).OptionalText(30);
    }
}

internal sealed class StatusPeriodRequestValidator : AbstractValidator<StatusPeriodRequest>
{
    public StatusPeriodRequestValidator()
    {
        RuleFor(r => r.Status).NotEqual(EmployeeStatus.Active).WithMessage("Para volver a Activo usa «Reactivar».");
        RuleFor(r => r.EndDate).NotNull().When(r => r.Status is not EmployeeStatus.Terminated and not EmployeeStatus.Active)
            .WithMessage("Indica la fecha de fin.");
        RuleFor(r => r.EndDate).Must((r, end) => end is not { } e || e >= r.StartDate)
            .WithMessage("La fecha de fin es anterior a la de inicio.");
        RuleFor(r => r.Reason).Must(x => x.Clean() is not null).When(r => r.Status is EmployeeStatus.Suspended)
            .WithMessage("Indica el motivo.");
        RuleFor(r => r.Reason).Must(x => StaffRules.TerminationReasons.Contains(x.Clean())).When(r => r.Status is EmployeeStatus.Terminated)
            .WithMessage("Elige el motivo de la baja.");
        RuleFor(r => r.Reason).OptionalText(200);
        RuleFor(r => r.Reference).OptionalText(40);
    }
}

internal sealed class UpdateStatusPeriodRequestValidator : AbstractValidator<UpdateStatusPeriodRequest>
{
    public UpdateStatusPeriodRequestValidator() => RuleFor(r => r.Reference).OptionalText(40);
}
