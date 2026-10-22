using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Security;
using Platform.Core.Users;

namespace Platform.Api.Internal;

/// <summary>A scheduled job: idempotent and safe to re-run (ARCHITECTURE §4). Registered by name.</summary>
public interface IJob
{
    string Name { get; }
    Task<object> RunAsync(CancellationToken cancellationToken);
}

public sealed record BootstrapRequest(string Email, string DisplayName);

public sealed record BootstrapResponse(Guid Id, string Email, string PasswordSetupLink);

public sealed class BootstrapRequestValidator : AbstractValidator<BootstrapRequest>
{
    public BootstrapRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("Este campo es obligatorio.").EmailAddress().WithMessage("Introduce un correo válido.");
        RuleFor(r => r.DisplayName).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(120).WithMessage("Admite hasta 120 caracteres.");
    }
}

/// <summary><c>/internal/*</c>: called by the scheduler container and by scripts, authenticated by <c>INTERNAL_SECRET</c>.</summary>
public static class InternalEndpoints
{
    public const string SecretHeader = "X-Internal-Secret";

    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder app, string internalSecret)
    {
        var group = app.MapGroup("/internal").ExcludeFromDescription()
            .AddEndpointFilter(async (context, next) =>
                IsAuthorized(context.HttpContext.Request.Headers[SecretHeader].ToString(), internalSecret)
                    ? await next(context)
                    : ApiResults.Unauthorized("Secreto interno inválido."));

        group.MapPost("/jobs/{name}", async (string name, IEnumerable<IJob> jobs, CancellationToken ct) =>
            jobs.FirstOrDefault(j => j.Name == name) is { } job
                ? Results.Ok(await job.RunAsync(ct))
                : ApiResults.NotFound($"Job '{name}' does not exist."));

        // First administrator of a new installation (scripts/bootstrap). Refuses once any administrator exists — except re-running for
        // the same email while that sole administrator has never signed in, which only issues a fresh link (the first run may have
        // committed the row and then failed to produce the link).
        group.MapPost("/bootstrap", async (BootstrapRequest body, DbContext db, IAuthProvider auth, CancellationToken ct) =>
            {
                var address = body.Email.Trim().ToLowerInvariant();
                var admins = await db.Set<User>().AsNoTracking().Where(u => u.Role == UserRole.Administrator).ToListAsync(ct);
                if (admins.Count > 0)
                {
                    if (admins is not [{ LastLoginAt: null, AcceptedAt: null } pending] || pending.Email != address)
                        return ApiResults.Conflict("Ya existe un administrador; invita usuarios desde Usuarios.");
                    return Results.Ok(new BootstrapResponse(pending.Uid, pending.Email, await auth.CreatePasswordSetupLinkAsync(pending.Email, ct)));
                }

                var authUid = await auth.CreateUserAsync(address, body.DisplayName.Trim(), ct);
                var user = new User
                {
                    AuthUid = authUid,
                    Email = address,
                    DisplayName = body.DisplayName.Trim(),
                    Role = UserRole.Administrator,
                    InvitedAt = DateTimeOffset.UtcNow,
                    // The owner holds every grant, including Reportes (SPEC §2.2, D5).
                    Permissions = PermissionModules.All.Select(m => new UserPermission { Module = m }).ToList(),
                };
                db.Add(user);
                await db.SaveChangesAsync(ct);
                var link = await auth.CreatePasswordSetupLinkAsync(user.Email, ct);
                return Results.Ok(new BootstrapResponse(user.Uid, user.Email, link));
            })
            .Validate<BootstrapRequest>();

        // Restore drill and go-live (docs/HANDOFF.md): can this ENCRYPTION_KEY read what the database holds? Decrypts one stored
        // account number and answers ok / failed — never the value.
        group.MapGet("/health/encryption", async (DbContext db, IFieldCipher cipher, CancellationToken ct) =>
            {
                var stored = await db.Database.SqlQuery<byte[]>($"""
                        SELECT account_number_enc AS "Value" FROM platform.person_bank_accounts
                        UNION ALL SELECT charge_account_number_enc FROM platform.paying_accounts
                        LIMIT 1
                        """).ToListAsync(ct);
                if (stored.Count == 0)
                    return Results.Ok(new { status = "no-data", detail = "Aún no hay valores cifrados que probar." });
                try
                {
                    _ = cipher.Decrypt(stored[0]);
                    return Results.Ok(new { status = "ok", keyId = (int)stored[0][0] });
                }
                catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or InvalidOperationException or ArgumentException)
                {
                    return Results.Json(new { status = "failed", detail = "La llave no descifra los datos guardados: no es la misma con la que se cifraron." },
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            });

        return app;
    }

    private static bool IsAuthorized(string provided, string expected) =>
        expected.Length >= 32 &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
