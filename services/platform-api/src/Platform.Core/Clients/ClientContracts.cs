using FluentValidation;
using Platform.Core.Http;
using Platform.Core.People;

namespace Platform.Core.Clients;

public sealed record ContactDto(Guid Id, string FullName, string? Email, string? Phone, string? JobTitle, bool IsPrimary,
    Guid? UserId)
{
    public static ContactDto From(ClientContact c) => new(c.Uid, c.FullName, c.Email, c.Phone, c.JobTitle, c.IsPrimary, c.UserId);
}

public sealed record SiteDto(Guid Id, string Name, Guid? ZoneId, string? ZoneName, string? Street, string? ExtNumber,
    string? Neighborhood, string? PostalCode, string? Municipality, string? State, bool IsActive);

/// <summary>A row of the Clientes lists. <see cref="Zone"/> is the zone of the client's first active site.</summary>
public sealed record ClientListItemDto(Guid Id, string ShortName, string? LegalName, string? Rfc, bool UsesRecruiting,
    bool UsesPayroll, bool IsActive, string? Zone, ContactDto? PrimaryContact);

/// <summary>An entry of the client picker (Usuarios, scoped grants).</summary>
public sealed record ClientOptionDto(Guid Id, string ShortName, bool IsActive);

public sealed record ClientDetailDto(Guid Id, string ShortName, string? LegalName, string? Rfc, string? Notes,
    bool UsesRecruiting, bool UsesPayroll, bool IsActive, IReadOnlyList<ContactDto> Contacts, IReadOnlyList<SiteDto> Sites);

public sealed record NewContactFields(string FullName, string? Email, string? Phone);

/// <summary>"Nuevo cliente" (both module entries, SPEC §3.3): no module choice; the route decides the usage marker.</summary>
public sealed record CreateClientRequest(string ShortName, string? LegalName, string? Rfc, NewContactFields? PrimaryContact);

public sealed record UpdateClientRequest(string ShortName, string? LegalName, string? Rfc, string? Notes, bool IsActive);

public sealed record ContactRequest(string FullName, string? Email, string? Phone, string? JobTitle, bool IsPrimary);

public sealed record SiteRequest(string Name, Guid? ZoneId, string? Street, string? ExtNumber, string? Neighborhood,
    string? PostalCode, string? Municipality, string? State, bool IsActive = true);

internal static class ClientRules
{
    public static void ClientFields<T>(this AbstractValidator<T> v, Func<T, string> shortName, Func<T, string?> rfc)
    {
        v.RuleFor(r => shortName(r)).NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(40).WithMessage("El nombre corto admite hasta 40 caracteres.").OverridePropertyName("shortName");
        v.RuleFor(r => Normalize(rfc(r)))
            .Must(x => x is null || MexicanIds.IsCompanyRfc(x) || MexicanIds.IsPersonRfc(x))
            .WithMessage("El RFC tiene 12 caracteres (persona moral) o 13 (persona física).").OverridePropertyName("rfc");
    }

    public static string? Normalize(string? rfc) => rfc.Clean()?.ToUpperInvariant();
}

internal sealed class NewContactFieldsValidator : AbstractValidator<NewContactFields>
{
    public NewContactFieldsValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().WithMessage("El nombre del contacto es obligatorio.").MaximumLength(120).WithMessage("Admite hasta 120 caracteres.");
        RuleFor(r => r.Email).OptionalEmail();
        RuleFor(r => r.Phone).OptionalMobile();
    }
}

internal sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        this.ClientFields(r => r.ShortName, r => r.Rfc);
        RuleFor(r => r.LegalName).OptionalText(200);
        RuleFor(r => r.PrimaryContact!).SetValidator(new NewContactFieldsValidator()).When(r => r.PrimaryContact is not null);
    }
}

internal sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        this.ClientFields(r => r.ShortName, r => r.Rfc);
        RuleFor(r => r.LegalName).OptionalText(200);
        RuleFor(r => r.Notes).OptionalText(2000);
    }
}

internal sealed class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().WithMessage("El nombre del contacto es obligatorio.").MaximumLength(120).WithMessage("Admite hasta 120 caracteres.");
        RuleFor(r => r.Email).OptionalEmail();
        RuleFor(r => r.Phone).OptionalMobile();
        RuleFor(r => r.JobTitle).OptionalText(80);
    }
}

internal sealed class SiteRequestValidator : AbstractValidator<SiteRequest>
{
    public SiteRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("El nombre del sitio es obligatorio.").MaximumLength(120).WithMessage("Admite hasta 120 caracteres.");
        RuleFor(r => r.PostalCode).OptionalPostalCode();
        RuleFor(r => r.Street).OptionalText(120);
        RuleFor(r => r.ExtNumber).OptionalText(20);
        RuleFor(r => r.Neighborhood).OptionalText(120);
        RuleFor(r => r.Municipality).OptionalText(80);
        RuleFor(r => r.State).OptionalText(60);
    }
}
