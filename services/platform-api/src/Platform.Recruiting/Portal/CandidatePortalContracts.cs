using FluentValidation;
using Platform.Core.People;

namespace Platform.Recruiting.Portal;

/// <summary>
/// «Mi perfil» (SPEC §7): the person's own data, without the recruiter-internal fields (fuente, observaciones, extranjero).
/// The email is how the candidate signs in, so it is shown but not edited here.
/// </summary>
public sealed record CandidateProfileDto(
    Guid Id,
    string FirstNames,
    string LastNamePaternal,
    string? LastNameMaternal,
    DateOnly? BirthDate,
    string? BirthState,
    Gender Gender,
    string? Curp,
    string? Rfc,
    string? Nss,
    string? MobilePhone,
    string? Email,
    string? Street,
    string? ExtNumber,
    string? Neighborhood,
    string? PostalCode,
    string? Municipality,
    string? State,
    DateTimeOffset? PrivacyNoticeAcceptedAt)
{
    public static CandidateProfileDto From(Person p) => new(p.Uid, p.FirstNames, p.LastNamePaternal, p.LastNameMaternal,
        p.BirthDate, p.BirthState, p.Gender, p.Curp, p.Rfc, p.Nss, p.MobilePhone, p.Email, p.Street, p.ExtNumber, p.Neighborhood,
        p.PostalCode, p.Municipality, p.State, p.PrivacyNoticeAcceptedAt);
}

public sealed record CandidateProfileRequest(
    string FirstNames,
    string LastNamePaternal,
    string? LastNameMaternal,
    DateOnly? BirthDate,
    string? BirthState,
    Gender? Gender,
    string? Curp,
    string? Rfc,
    string? Nss,
    string? MobilePhone,
    string? Street,
    string? ExtNumber,
    string? Neighborhood,
    string? PostalCode,
    string? Municipality,
    string? State)
{
    /// <summary>The registry's request, keeping what the candidate cannot change from <paramref name="current"/>.</summary>
    public PersonRequest ToPersonRequest(Person? current) => new(FirstNames, LastNamePaternal, LastNameMaternal, BirthDate,
        BirthState, Gender, current?.IsForeign ?? false, Curp, Rfc, Nss, MobilePhone, current?.Email, Street, ExtNumber,
        Neighborhood, PostalCode, Municipality, State, current?.Source, current?.Notes);
}

/// <summary>The friendly tracker of «Mi postulación»: Recibida → Contactado → Entrevista → Resultado (prototype).</summary>
public enum ApplicationStep
{
    Received,
    Contacted,
    Interview,
    Result,
}

/// <summary>
/// What the candidate is told, never the internal stage: in process, selected (Apto: presented to the company), hired, or
/// closed (No apto, Descartado, or the vacante cancelled or filled by others) — reasons stay internal.
/// </summary>
public enum ApplicationOutcome
{
    InProcess,
    Selected,
    Hired,
    Closed,
}

public sealed record CandidateApplicationDto(
    Guid Id,
    string OpeningName,
    ApplicationStep Step,
    ApplicationOutcome Outcome,
    DateOnly CapturedAt,
    DateOnly? HiredAt,
    string? RecruiterName,
    string? RecruiterPhone,
    string? RecruiterEmail,
    DateTimeOffset? NextInterviewAt,
    string? NextInterviewLocation);

internal sealed class CandidateProfileRequestValidator : AbstractValidator<CandidateProfileRequest>
{
    private static readonly PersonRequestValidator Person = new();

    // Same rules as the registry (CURP format, birth date against the CURP, phone, CP…), reported on the same field names.
    public CandidateProfileRequestValidator() =>
        RuleFor(r => r).Custom((r, context) =>
        {
            foreach (var e in Person.Validate(r.ToPersonRequest(null)).Errors)
                context.AddFailure(e.PropertyName, e.ErrorMessage);
        });
}
