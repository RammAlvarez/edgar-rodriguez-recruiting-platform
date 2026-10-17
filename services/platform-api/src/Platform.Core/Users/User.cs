using Platform.Core.Abstractions;

namespace Platform.Core.Users;

/// <summary>DATA_MODEL §3.1. Roles live here, never in token claims.</summary>
public sealed class User : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string AuthUid { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public Guid? ClientId { get; set; }
    public bool MfaRequired { get; set; }
    /// <summary>The TOTP secret, encrypted like the account numbers (D89); set at «Activar», kept once enabled.</summary>
    public byte[]? MfaSecretEnc { get; set; }
    public DateTimeOffset? MfaEnabledAt { get; set; }
    /// <summary>The last code's time step: a code is good once.</summary>
    public long? MfaLastStep { get; set; }
    public int MfaFailedAttempts { get; set; }
    public DateTimeOffset? MfaLockedUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? InvitedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }

    public List<UserPermission> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §3.2. <see cref="ClientId"/> null = all clients.</summary>
public sealed class UserPermission : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public PermissionModule Module { get; set; }
    public Guid? ClientId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
