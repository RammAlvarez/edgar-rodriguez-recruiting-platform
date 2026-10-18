using FluentValidation;

namespace Platform.Core.Catalogs;

public sealed record ZoneDto(Guid Id, string NameEs, string State, bool IsActive)
{
    public static ZoneDto From(Zone z) => new(z.Uid, z.NameEs, z.State, z.IsActive);
}

public sealed record CatalogPositionDto(Guid Id, string NameEs, bool IsActive)
{
    public static CatalogPositionDto From(CatalogPosition p) => new(p.Uid, p.NameEs, p.IsActive);
}

public sealed record EducationLevelDto(Guid Id, string NameEs, int SortOrder, bool IsActive)
{
    public static EducationLevelDto From(EducationLevel e) => new(e.Uid, e.NameEs, e.SortOrder, e.IsActive);
}

public sealed record BenefitDto(Guid Id, string NameEs, bool IsDefault, bool IsActive)
{
    public static BenefitDto From(Benefit b) => new(b.Uid, b.NameEs, b.IsDefault, b.IsActive);
}

public sealed record HolidayDto(Guid Id, DateOnly Date, string NameEs, bool IsMandatory)
{
    public static HolidayDto From(Holiday h) => new(h.Uid, h.Date, h.NameEs, h.IsMandatory);
}

public sealed record BankDto(Guid Id, string Name, string SpeiCode, RegistrationBank? SupportsRegistration,
    bool SupportsMassivePayment, bool IsActive)
{
    public static BankDto From(Bank b) => new(b.Uid, b.Name, b.SpeiCode, b.SupportsRegistration, b.SupportsMassivePayment, b.IsActive);
}

public sealed record DocumentTypeDto(Guid Id, string Code, string NameEs, bool HasExpiry, int? DefaultValidityDays,
    int SortOrder, bool IsActive)
{
    public static DocumentTypeDto From(DocumentType d) =>
        new(d.Uid, d.Code, d.NameEs, d.HasExpiry, d.DefaultValidityDays, d.SortOrder, d.IsActive);
}

public sealed record DocumentTemplateItemDto(Guid DocumentTypeId, bool IsRequired);

public sealed record DocumentTemplateDto(Guid Id, string NameEs, DocumentTemplateScope Scope, bool IsDefault,
    IReadOnlyList<DocumentTemplateItemDto> Items)
{
    public static DocumentTemplateDto From(DocumentTemplate t) => new(t.Uid, t.NameEs, t.Scope, t.IsDefault,
        t.Items.Select(i => new DocumentTemplateItemDto(i.DocumentTypeId, i.IsRequired)).ToList());
}

/// <summary>Every catalog in one response: the forms of all screens read from it (tag <c>catalogs</c>).</summary>
public sealed record CatalogsDto(
    IReadOnlyList<ZoneDto> Zones,
    IReadOnlyList<CatalogPositionDto> Positions,
    IReadOnlyList<EducationLevelDto> EducationLevels,
    IReadOnlyList<BenefitDto> Benefits,
    IReadOnlyList<HolidayDto> Holidays,
    IReadOnlyList<BankDto> Banks,
    IReadOnlyList<DocumentTypeDto> DocumentTypes,
    IReadOnlyList<DocumentTemplateDto> DocumentTemplates);

public sealed record ZoneRequest(string NameEs, string State, bool IsActive = true);

public sealed record CatalogPositionRequest(string NameEs, bool IsActive = true);

public sealed record EducationLevelRequest(string NameEs, int SortOrder, bool IsActive = true);

public sealed record BenefitRequest(string NameEs, bool IsDefault, bool IsActive = true);

public sealed record HolidayRequest(DateOnly Date, string NameEs, bool IsMandatory);

public sealed record StatutoryHolidaysRequest(int Year);

public sealed record StatutoryHolidaysResult(int Added, IReadOnlyList<HolidayDto> Holidays);

public sealed record BankRequest(string Name, string SpeiCode, RegistrationBank? SupportsRegistration,
    bool SupportsMassivePayment, bool IsActive = true);

/// <summary><see cref="Code"/> is only read on create; it is the stable key other modules refer to.</summary>
public sealed record DocumentTypeRequest(string Code, string NameEs, bool HasExpiry, int? DefaultValidityDays,
    int SortOrder, bool IsActive = true);

public sealed record DocumentTemplateRequest(string NameEs, DocumentTemplateScope Scope, bool IsDefault,
    IReadOnlyList<DocumentTemplateItemDto> Items);

internal static class CatalogRules
{
    public static IRuleBuilderOptions<T, string> Name<T>(this IRuleBuilder<T, string> rule, int max = 120) =>
        rule.NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(max).WithMessage($"El nombre admite hasta {max} caracteres.");
}

internal sealed class ZoneRequestValidator : AbstractValidator<ZoneRequest>
{
    public ZoneRequestValidator()
    {
        RuleFor(r => r.NameEs).Name();
        RuleFor(r => r.State).NotEmpty().WithMessage("El estado es obligatorio.").MaximumLength(60).WithMessage("Admite hasta 60 caracteres.");
    }
}

internal sealed class CatalogPositionRequestValidator : AbstractValidator<CatalogPositionRequest>
{
    public CatalogPositionRequestValidator() => RuleFor(r => r.NameEs).Name();
}

internal sealed class EducationLevelRequestValidator : AbstractValidator<EducationLevelRequest>
{
    public EducationLevelRequestValidator()
    {
        RuleFor(r => r.NameEs).Name();
        RuleFor(r => r.SortOrder).GreaterThanOrEqualTo(0).WithMessage("El orden no puede ser negativo.");
    }
}

internal sealed class BenefitRequestValidator : AbstractValidator<BenefitRequest>
{
    public BenefitRequestValidator() => RuleFor(r => r.NameEs).Name();
}

internal sealed class HolidayRequestValidator : AbstractValidator<HolidayRequest>
{
    public HolidayRequestValidator()
    {
        RuleFor(r => r.NameEs).Name();
        RuleFor(r => r.Date).NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.");
    }
}

internal sealed class StatutoryHolidaysRequestValidator : AbstractValidator<StatutoryHolidaysRequest>
{
    public StatutoryHolidaysRequestValidator() =>
        RuleFor(r => r.Year).InclusiveBetween(2020, 2100).WithMessage("Indica un año entre 2020 y 2100.");
}

internal sealed class BankRequestValidator : AbstractValidator<BankRequest>
{
    public BankRequestValidator()
    {
        RuleFor(r => r.Name).Name(60);
        RuleFor(r => r.SpeiCode).Matches(@"^\d{3}$").WithMessage("La clave SPEI tiene 3 dígitos.");
    }
}

internal sealed class DocumentTypeRequestValidator : AbstractValidator<DocumentTypeRequest>
{
    public DocumentTypeRequestValidator()
    {
        RuleFor(r => r.Code).Matches("^[A-Z][A-Z0-9_]{1,29}$")
            .WithMessage("La clave va en mayúsculas, sin espacios (por ejemplo ACTA_NAC).");
        RuleFor(r => r.NameEs).Name();
        RuleFor(r => r.DefaultValidityDays).GreaterThan(0).When(r => r.DefaultValidityDays is not null)
            .WithMessage("La vigencia se indica en días, mayor a cero.");
        RuleFor(r => r.DefaultValidityDays).Null().When(r => !r.HasExpiry)
            .WithMessage("Solo un documento con vencimiento tiene vigencia.");
    }
}

internal sealed class DocumentTemplateRequestValidator : AbstractValidator<DocumentTemplateRequest>
{
    public DocumentTemplateRequestValidator()
    {
        RuleFor(r => r.NameEs).Name();
        RuleFor(r => r.Scope).IsInEnum();
        RuleFor(r => r.Items).NotEmpty().WithMessage("Agrega al menos un documento.")
            .Must(items => items.Select(i => i.DocumentTypeId).Distinct().Count() == items.Count)
            .WithMessage("Cada documento aparece una sola vez.");
    }
}
