using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Security;

namespace Platform.Core.Users;

/// <summary>A one-time recovery code (stored as its SHA-256; shown once at «Activar»).</summary>
public sealed class MfaRecoveryCode : IDerived
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

/// <summary>A sign-in (the provider's <c>auth_time</c>) whose code was entered: its requests pass the MFA check (PLAN D89).</summary>
public sealed class MfaSession : IDerived
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public long AuthTime { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}

internal sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> b)
    {
        b.ToTable("mfa_recovery_codes");
        b.HasIdentityAndUid();
        b.Property(c => c.CodeHash).HasMaxLength(64);
        b.HasIndex(c => new { c.UserId, c.CodeHash }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MfaSessionConfiguration : IEntityTypeConfiguration<MfaSession>
{
    public void Configure(EntityTypeBuilder<MfaSession> b)
    {
        b.ToTable("mfa_sessions");
        b.HasIdentityAndUid();
        b.HasIndex(s => new { s.UserId, s.AuthTime }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed record MfaSetupDto(string Secret, string OtpAuthUri);

public sealed record MfaCodeRequest(string Code);

public sealed record MfaRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// Verificación en dos pasos (SPEC §2.3, PLAN D89) in the API, whatever the identity provider: a staff user turns it on (or
/// must, when Usuarios requires it) with an authenticator app; afterwards every new sign-in asks the 6-digit code — or one of
/// the recovery codes — before anything else. Five wrong codes wait 15 minutes.
/// </summary>
public static class MfaEndpoints
{
    public const string Issuer = "Plataforma RyP";
    public const int RecoveryCodeCount = 8;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    public const string ProblemType = "https://platform.local/problems/mfa";
    public const string MustVerify = "Escribe el código de tu app de autenticación para continuar.";
    public const string MustEnroll = "Tu cuenta requiere verificación en dos pasos: actívala para continuar.";
    public const string WrongCode = "El código no es correcto o ya se usó. Escribe el que muestra tu app ahora.";
    public const string Locked = "Demasiados intentos. Espera 15 minutos e inténtalo de nuevo.";
    public const string AlreadyEnabled = "La verificación en dos pasos ya está activa.";
    public const string NotEnabled = "La verificación en dos pasos no está activa.";
    public const string StartFirst = "Primero genera el código QR.";
    public const string Required = "Tu cuenta requiere la verificación en dos pasos; solo un administrador puede quitarla.";

    public static readonly AccessRule Access = AccessRule.ForRoles(UserRole.Administrator, UserRole.Recruiter);

    /// <summary>The 403 the apps recognise by its <c>type</c>: enroll or enter the code.</summary>
    public static IResult Problem(bool enrolled) =>
        TypedResults.Problem(title: "Verificación en dos pasos", detail: enrolled ? MustVerify : MustEnroll,
            statusCode: StatusCodes.Status403Forbidden, type: ProblemType);

    public static IEndpointRouteBuilder MapMfaEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/me/mfa").WithTags("Users");

        // A new secret for the QR; «Activar» confirms it with a code. Starting again replaces it.
        g.MapPost("/setup", async (DbContext db, RequestContext ctx, IFieldCipher cipher, CancellationToken ct) =>
            {
                var user = await Me(db, ctx, ct);
                if (user.MfaEnabledAt is not null)
                    return ApiResults.Conflict(AlreadyEnabled);
                var secret = Totp.NewSecret();
                user.MfaSecretEnc = cipher.Encrypt(Base32.Encode(secret));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new MfaSetupDto(Base32.Encode(secret), Totp.Uri(Issuer, user.Email, secret)));
            })
            .WithName("SetupMfa").Produces<MfaSetupDto>().ProducesProblem(StatusCodes.Status409Conflict).Invalidates("me").RequireAccess(Access);

        g.MapPost("/enable", async (MfaCodeRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher, IClock clock, CancellationToken ct) =>
            {
                var user = await Me(db, ctx, ct);
                if (user.MfaEnabledAt is not null)
                    return ApiResults.Conflict(AlreadyEnabled);
                if (user.MfaSecretEnc is null)
                    return ApiResults.Conflict(StartFirst);
                if (await CheckAsync(db, user, cipher, r.Code, clock.UtcNow, recovery: false, ct) is { } problem)
                    return problem;
                user.MfaEnabledAt = clock.UtcNow;
                var codes = Enumerable.Range(0, RecoveryCodeCount).Select(_ => NewRecoveryCode()).ToList();
                await db.Set<MfaRecoveryCode>().Where(c => c.UserId == user.Uid).ExecuteDeleteAsync(ct);
                db.AddRange(codes.Select(c => new MfaRecoveryCode { UserId = user.Uid, CodeHash = Hash(c) }));
                await MarkVerifiedAsync(db, user, ctx.AuthTime, clock.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new MfaRecoveryCodesDto(codes));
            })
            .WithName("EnableMfa").Produces<MfaRecoveryCodesDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem()
            .Invalidates("me").RequireAccess(Access);

        g.MapPost("/verify", async (MfaCodeRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher, IClock clock, CancellationToken ct) =>
            {
                var user = await Me(db, ctx, ct);
                if (user.MfaEnabledAt is null)
                    return ApiResults.Conflict(NotEnabled);
                if (await CheckAsync(db, user, cipher, r.Code, clock.UtcNow, recovery: true, ct) is { } problem)
                    return problem;
                await MarkVerifiedAsync(db, user, ctx.AuthTime, clock.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("VerifyMfa").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem().Invalidates("me").RequireAccess(Access);

        g.MapPost("/disable", async (MfaCodeRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher, IClock clock, CancellationToken ct) =>
            {
                var user = await Me(db, ctx, ct);
                if (user.MfaEnabledAt is null)
                    return ApiResults.Conflict(NotEnabled);
                if (user.MfaRequired)
                    return ApiResults.Conflict(Required);
                if (await CheckAsync(db, user, cipher, r.Code, clock.UtcNow, recovery: true, ct) is { } problem)
                    return problem;
                await ResetAsync(db, user, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DisableMfa").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem().Invalidates("me").RequireAccess(Access);

        return v1;
    }

    /// <summary>
    /// The code (or, where allowed, a recovery code) for <paramref name="user"/>; null when right. Counts wrong ones and locks
    /// after <see cref="MaxAttempts"/>. Saves the attempt count.
    /// </summary>
    private static async Task<IResult?> CheckAsync(DbContext db, User user, IFieldCipher cipher, string? code, DateTimeOffset now, bool recovery,
        CancellationToken ct)
    {
        if (user.MfaLockedUntil is { } until && until > now)
            return ApiResults.Validation(new Dictionary<string, string[]> { ["code"] = [Locked] });
        var secret = Base32.Decode(cipher.Decrypt(user.MfaSecretEnc!));
        if (Totp.Verify(secret, code ?? "", now, user.MfaLastStep) is { } step)
        {
            user.MfaLastStep = step;
            user.MfaFailedAttempts = 0;
            user.MfaLockedUntil = null;
            return null;
        }
        if (recovery && NormalizeRecovery(code) is { Length: 8 } normalized)
        {
            var hash = Hash(normalized);
            var match = await db.Set<MfaRecoveryCode>().SingleOrDefaultAsync(c => c.UserId == user.Uid && c.CodeHash == hash && c.UsedAt == null, ct);
            if (match is not null)
            {
                match.UsedAt = now;
                user.MfaFailedAttempts = 0;
                user.MfaLockedUntil = null;
                return null;
            }
        }
        user.MfaFailedAttempts++;
        if (user.MfaFailedAttempts >= MaxAttempts)
        {
            user.MfaFailedAttempts = 0;
            user.MfaLockedUntil = now.Add(LockFor);
        }
        await db.SaveChangesAsync(ct);
        return ApiResults.Validation(new Dictionary<string, string[]> { ["code"] = [user.MfaLockedUntil > now ? Locked : WrongCode] });
    }

    private static async Task MarkVerifiedAsync(DbContext db, User user, long? signedInAt, DateTimeOffset now, CancellationToken ct)
    {
        if (signedInAt is not { } authTime)
            return;
        if (!await db.Set<MfaSession>().AnyAsync(s => s.UserId == user.Uid && s.AuthTime == authTime, ct))
            db.Add(new MfaSession { UserId = user.Uid, AuthTime = authTime, VerifiedAt = now });
    }

    /// <summary>Turns it off for the user (their «Desactivar», or Usuarios' «Restablecer» for a lost phone).</summary>
    public static async Task ResetAsync(DbContext db, User user, CancellationToken ct)
    {
        user.MfaSecretEnc = null;
        user.MfaEnabledAt = null;
        user.MfaLastStep = null;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        await db.Set<MfaRecoveryCode>().Where(c => c.UserId == user.Uid).ExecuteDeleteAsync(ct);
        await db.Set<MfaSession>().Where(s => s.UserId == user.Uid).ExecuteDeleteAsync(ct);
    }

    private static Task<User> Me(DbContext db, RequestContext ctx, CancellationToken ct) =>
        db.Set<User>().SingleAsync(u => u.Uid == ctx.RequiredPrincipal.UserId, ct);

    /// <summary>«K7Q2-M9XD»: 8 characters of base32 (40 bits), dash for reading.</summary>
    private static string NewRecoveryCode()
    {
        var code = Base32.Encode(RandomNumberGenerator.GetBytes(5));
        return $"{code[..4]}-{code[4..]}";
    }

    private static string? NormalizeRecovery(string? code) =>
        code is null ? null : new string(code.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeRecovery(code)!)));
}
