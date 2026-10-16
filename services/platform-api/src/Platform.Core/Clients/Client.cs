using Platform.Core.Abstractions;

namespace Platform.Core.Clients;

/// <summary>DATA_MODEL §3.3. Routes arrive in slice S3; the table exists from S0 because users and grants are scoped by client.</summary>
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

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
