using FluentValidation;
using Platform.Core.Http;

namespace Platform.Core.People;

public sealed record PersonSummaryDto(Guid Id, string FullName, string? Curp, string? Rfc, string? MobilePhone, string? Email);

public sealed record PersonDto(
    Guid Id,
    string FirstNames,
    string LastNamePaternal,
    string? LastNameMaternal,
    string FullName,
    DateOnly? BirthDate,
    string? BirthState,
    Gender Gender,
    bool IsForeign,
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
    CandidateSource? Source,
    string? Notes)
{
    public static PersonDto From(Person p) => new(p.Uid, p.FirstNames, p.LastNamePaternal, p.LastNameMaternal, p.FullName,
        p.BirthDate, p.BirthState, p.Gender, p.IsForeign, p.Curp, p.Rfc, p.Nss, p.MobilePhone, p.Email, p.Street, p.ExtNumber,
        p.Neighborhood, p.PostalCode, p.Municipality, p.State, p.Source, p.Notes);
}

/// <summary>A person already registered with the same CURP or RFC (<see cref="IsDeleted"/>: offer to restore, not to duplicate).</summary>
public sealed record DuplicatePersonDto(Guid Id, string FullName, string? Curp, string? Rfc, bool IsDeleted,
    IReadOnlyList<string> MatchedOn);

public sealed record PersonRequest(
    string FirstNames,
    string LastNamePaternal,
    string? LastNameMaternal,
    DateOnly? BirthDate,
    string? BirthState,
    Gender? Gender,
    bool IsForeign,
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
    CandidateSource? Source,
    string? Notes);

public sealed class PersonRequestValidator : AbstractValidator<PersonRequest>
{
    public PersonRequestValidator()
    {
        RuleFor(r => r.FirstNames).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(80);
        RuleFor(r => r.LastNamePaternal).NotEmpty().WithMessage("El apellido paterno es obligatorio.").MaximumLength(60);
        RuleFor(r => r.LastNameMaternal).OptionalText(60);
        RuleFor(r => PersonText.Id(r.Curp)).Must(c => c is null || MexicanIds.IsCurp(c))
            .WithMessage("La CURP debe tener 18 caracteres con el formato oficial.").OverridePropertyName("curp");
        RuleFor(r => PersonText.Id(r.Rfc)).Must(c => c is null || MexicanIds.IsPersonRfc(c))
            .WithMessage("El RFC de persona física debe tener 13 caracteres.").OverridePropertyName("rfc");
        RuleFor(r => r.Nss).Matches(@"^\d{11}$").WithMessage("El NSS tiene 11 dígitos.");
        RuleFor(r => r.MobilePhone).OptionalMobile();
        RuleFor(r => r.Email).OptionalEmail();
        RuleFor(r => r.PostalCode).OptionalPostalCode();
        RuleFor(r => r.Street).OptionalText(120);
        RuleFor(r => r.ExtNumber).OptionalText(20);
        RuleFor(r => r.Neighborhood).OptionalText(120);
        RuleFor(r => r.Municipality).OptionalText(80);
        RuleFor(r => r.State).OptionalText(60);
        RuleFor(r => r.BirthState).OptionalText(60);
        RuleFor(r => r.Notes).OptionalText(2000);
        RuleFor(r => r.Gender).IsInEnum();
        RuleFor(r => r.Source).IsInEnum();
        // The bank files (Banregio FechaNac) are rejected when the birth date and the CURP disagree.
        RuleFor(r => r.BirthDate)
            .Must((r, birth) => birth is null || PersonText.Id(r.Curp) is not { } c || !MexicanIds.IsCurp(c)
                || MexicanIds.CurpBirthDate(c) == birth)
            .WithMessage("La fecha de nacimiento no coincide con la CURP.");
    }
}

public static class PersonText
{
    /// <summary>Names as the client's files store them: uppercase, single spaces (accents kept).</summary>
    public static string Name(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static string? OptionalName(string? value) => value.Clean() is { } v ? Name(v) : null;

    /// <summary>CURP / RFC: trimmed, uppercase, no inner spaces; empty → null.</summary>
    public static string? Id(string? value) => value.Clean()?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}
