using Microsoft.EntityFrameworkCore;
using Platform.Core.Notifications;
using Platform.Core.People;
using Platform.Core.Users;

namespace Platform.Recruiting.Candidates;

/// <summary>A candidate uploaded a document: each recruiter of their applications is told (SPEC §3.5).</summary>
public sealed class CandidateDocumentListener : ICandidateDocumentListener
{
    public async Task UploadedAsync(DbContext db, Guid personId, Guid documentId, string documentName, DateTimeOffset now, CancellationToken ct)
    {
        var name = await db.Set<Person>().Where(p => p.Uid == personId).Select(p => p.FullName).SingleAsync(ct);
        var applications = await db.Set<Candidate>().Where(c => c.PersonId == personId && c.RecruiterId != null)
            .Join(db.Set<User>(), c => c.RecruiterId, u => u.Uid, (c, u) => new { Candidate = c.Uid, User = u.Uid, u.Role })
            .ToListAsync(ct);
        foreach (var a in applications)
            await Notifier.NotifyAsync(db, [a.User], new NotificationDraft(NotificationKind.DocumentUploaded, $"doc-uploaded:{documentId}",
                $"{name} subió {documentName}", "Revísalo y valídalo o recházalo.",
                a.Role == UserRole.Recruiter ? $"/mis-candidatos/{a.Candidate}" : $"/candidatos/{a.Candidate}"), now, ct);
    }
}
