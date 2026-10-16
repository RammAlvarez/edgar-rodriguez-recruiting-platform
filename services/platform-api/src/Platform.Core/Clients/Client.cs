using Platform.Core.Abstractions;

namespace Platform.Core.Clients;

/// <summary>
/// DATA_MODEL §3.3. <see cref="UsesRecruiting"/> / <see cref="UsesPayroll"/> are usage markers set by the server (the entry
/// the client was created from, its first vacante or payroll group), never asked in the UI (SPEC §3.3).
/// </summary>
public sealed class Client : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string ShortName { get; set; }
    public string? LegalName { get; set; }
    public string? Rfc { get; set; }
    public bool UsesRecruiting { get; set; }
    public bool UsesPayroll { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public List<ClientContact> Contacts { get; set; } = [];
    public List<ClientSite> Sites { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.4. <see cref="UserId"/> is set when the contact is invited to the Clients app.</summary>
public sealed class ClientContact : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid ClientId { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? UserId { get; set; }
    public bool IsPrimary { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.5: a site / sucursal; the client's zone is the zone of its sites.</summary>
public sealed class ClientSite : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid ClientId { get; set; }
    public required string Name { get; set; }
    public Guid? ZoneId { get; set; }
    public string? Street { get; set; }
    public string? ExtNumber { get; set; }
    public string? Neighborhood { get; set; }
    public string? PostalCode { get; set; }
    public string? Municipality { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
