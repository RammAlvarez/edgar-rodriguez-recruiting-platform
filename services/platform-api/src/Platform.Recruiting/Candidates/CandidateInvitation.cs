using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Candidates;

/// <summary>«Reenviar invitación»: whether the email went out, and to which address.</summary>
public sealed record CandidateInvitationResultDto(bool Sent, string Email);

/// <summary>
/// The capture invitation to the Candidates app (SPEC §4.3 Nuevo, ARCHITECTURE §4.1 step 1, PLAN D77): sent after the commit
/// when the person has an email; a failed send is logged and can be repeated from the candidate detail, never rolled back.
/// The link is the app's URL: the candidate types the same email there and gets a sign-in link (no password).
/// </summary>
public static partial class CandidateInvitation
{
    public const string NoEmail = "La persona no tiene correo; agrégalo en Datos personales para invitarla.";

    public static void Map(RouteGroupBuilder candidates) =>
        candidates.MapPost("/{id:guid}/invitation", async (Guid id, DbContext db, RequestContext ctx, IEmailSender email,
                AppLinks links, ILoggerFactory logs, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                var address = await db.Set<Candidate>().Where(c => c.Uid == id)
                    .Join(db.Set<Person>(), c => c.PersonId, p => p.Uid, (c, p) => p.Email).SingleAsync(ct);
                if (address is null)
                    return ApiResults.Conflict(NoEmail);
                return TypedResults.Ok(new CandidateInvitationResultDto(await SendAsync(db, email, links, logs, id, ct), address));
            })
            .WithName("ResendCandidateInvitation").Produces<CandidateInvitationResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAccess(RecruitingScope.Staff).Invalidates(CandidateEndpoints.Tag);

    /// <summary>Sends the invitation of one application; <c>false</c> when there is no email or the provider failed.</summary>
    public static async Task<bool> SendAsync(DbContext db, IEmailSender email, AppLinks links, ILoggerFactory logs, Guid candidateId,
        CancellationToken ct)
    {
        var row = await db.Set<Candidate>().AsNoTracking().Where(c => c.Uid == candidateId)
            .Join(db.Set<Person>(), c => c.PersonId, p => p.Uid, (c, p) => new { c.JobOpeningId, c.RecruiterId, p.FirstNames, p.LastNamePaternal, p.Email })
            .SingleAsync(ct);
        if (row.Email is null)
            return false;
        try
        {
            var opening = (await OpeningQueries.DisplayNames(db, [row.JobOpeningId], ct))[row.JobOpeningId];
            var recruiter = row.RecruiterId is { } r
                ? await db.Set<User>().Where(u => u.Uid == r).Select(u => u.DisplayName).SingleOrDefaultAsync(ct)
                : null;
            await email.SendAsync(Build(row.Email, PersonText.Display(row.FirstNames), opening, recruiter, links.Candidates), ct);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Failed(logs.CreateLogger(typeof(CandidateInvitation)), e, candidateId);
            return false;
        }
    }

    public static EmailMessage Build(string to, string firstNames, string opening, string? recruiter, string appUrl)
    {
        var subject = $"Tu postulación: {opening}";
        var text = $"""
            Hola {firstNames}:

            Te registramos en el proceso de la vacante {opening}.{(recruiter is null ? "" : $" Te acompaña {recruiter}.")}
            En «Mi postulación» puedes completar tus datos, subir tus documentos desde el celular y ver cómo va tu proceso.
            Entra con este correo ({to}); te enviaremos un enlace para iniciar sesión, sin contraseña.

            {appUrl}
            """;
        static string e(string value) => WebUtility.HtmlEncode(value);
        var html = $"""
            <p>Hola {e(firstNames)}:</p>
            <p>Te registramos en el proceso de la vacante <strong>{e(opening)}</strong>.{(recruiter is null ? "" : $" Te acompaña {e(recruiter)}.")}<br>
            En «Mi postulación» puedes completar tus datos, subir tus documentos desde el celular y ver cómo va tu proceso.</p>
            <p><a href="{e(appUrl)}">Abrir Mi postulación</a></p>
            <p style="color:#555">Entra con este correo ({e(to)}); te enviaremos un enlace para iniciar sesión, sin contraseña.</p>
            """;
        return new EmailMessage(to, subject, html, text);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Invitation email of candidate {CandidateId} failed")]
    private static partial void Failed(ILogger logger, Exception exception, Guid candidateId);
}
