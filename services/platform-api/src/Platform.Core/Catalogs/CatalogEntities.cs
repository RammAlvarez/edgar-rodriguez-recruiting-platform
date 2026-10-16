using Platform.Core.Abstractions;

namespace Platform.Core.Catalogs;

/// <summary>Postgres enum <c>registration_bank</c>: banks whose alta masiva layout the platform writes.</summary>
public enum RegistrationBank
{
    Banorte,
    Banregio,
}

/// <summary>Postgres enum <c>document_template_scope</c> (DATA_MODEL §3.9).</summary>
public enum DocumentTemplateScope
{
    Candidate,
    Employee,
}

/// <summary>DATA_MODEL §3.11 <c>zones</c>: municipios de Nuevo León (seeded) plus custom ones.</summary>
public sealed class Zone : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string NameEs { get; set; }
    public required string State { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.11 <c>positions_catalog</c>: puestos on the recruiting side (payroll positions are per group, §5.2).</summary>
public sealed class CatalogPosition : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string NameEs { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.11 <c>education_levels</c>.</summary>
public sealed class EducationLevel : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string NameEs { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.11 <c>benefits</c> (prestaciones of a vacante).</summary>
public sealed class Benefit : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string NameEs { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.11 <c>holidays</c>: the festivo of the pre-payroll engine is attendance on one of these dates (D51).</summary>
public sealed class Holiday : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public DateOnly Date { get; set; }
    public required string NameEs { get; set; }
    public bool IsMandatory { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.8 <c>banks</c>, keyed by the Banxico SPEI code.</summary>
public sealed class Bank : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public required string SpeiCode { get; set; }
    public RegistrationBank? SupportsRegistration { get; set; }
    public bool SupportsMassivePayment { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.9 <c>document_types</c>; <see cref="Code"/> is the stable key (INE, CURP, …).</summary>
public sealed class DocumentType : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameEs { get; set; }
    public bool HasExpiry { get; set; }
    public int? DefaultValidityDays { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.9 <c>document_templates</c>: the checklist a vacante or a position asks for.</summary>
public sealed class DocumentTemplate : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string NameEs { get; set; }
    public DocumentTemplateScope Scope { get; set; }
    public bool IsDefault { get; set; }

    public List<DocumentTemplateItem> Items { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.9 <c>document_template_items</c>: one document type in a template, required or optional.</summary>
public sealed class DocumentTemplateItem : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid TemplateId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public bool IsRequired { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
