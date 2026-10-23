using System.Globalization;
using System.Net;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>Inicio's documents about to expire: only current, not rejected, within 30 days, soonest first; Administrador with Reclutamiento only.</summary>
public sealed class DashboardTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Lists_the_documents_that_expire_within_30_days_soonest_first()
    {
        var today = new SystemClock().Today();
        var type = await WithDb(async db =>
        {
            var t = new DocumentType { Code = $"DASH_{Guid.NewGuid().ToString("N").ToUpperInvariant()}", NameEs = "INE", HasExpiry = true };
            db.Add(t);
            await db.SaveChangesAsync();
            return t;
        });
        var late = await WithDb(db => db.AddPerson("LUIS", "PADILLA", null));
        var soon = await WithDb(db => db.AddPerson("ANA", "BAUTISTA", null));
        var far = await WithDb(db => db.AddPerson("PEDRO", "SALAZAR", null));
        var rejected = await WithDb(db => db.AddPerson("RITA", "RECHAZADA", null));
        var old = await WithDb(db => db.AddPerson("OMAR", "REEMPLAZADO", null));
        await WithDb(async db =>
        {
            db.Add(Doc(late.Uid, type.Uid, today.AddDays(25)));
            db.Add(Doc(soon.Uid, type.Uid, today.AddDays(3)));
            db.Add(Doc(far.Uid, type.Uid, today.AddDays(45)));
            db.Add(Doc(rejected.Uid, type.Uid, today.AddDays(5), DocumentStatus.Rejected));
            // The old one was replaced by a newer document that expires far away: only the newer one counts.
            var newer = Doc(old.Uid, type.Uid, today.AddDays(400));
            db.Add(newer);
            await db.SaveChangesAsync();
            db.Add(Doc(old.Uid, type.Uid, today.AddDays(6), supersededBy: newer.Uid));
            await db.SaveChangesAsync();
        });

        var owner = await AsOwner();
        var response = await owner.GetAsync("/v1/dashboard/expiring-documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = new[] { late.Uid, soon.Uid, far.Uid, rejected.Uid, old.Uid };
        var mine = (await BodyOf(response)).EnumerateArray().Where(r => ids.Contains(r.GetProperty("personId").GetGuid())).ToList();

        Assert.Equal([soon.Uid, late.Uid], mine.Select(r => r.GetProperty("personId").GetGuid()));
        Assert.Equal("INE", mine[0].GetProperty("documentTypeName").GetString());
        Assert.Equal(today.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), mine[0].GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task Needs_an_Administrador_with_Reclutamiento()
    {
        var payrollOnly = await As(UserRole.Administrator, PermissionModule.PayrollAttendance);
        Assert.Equal(HttpStatusCode.Forbidden, (await payrollOnly.GetAsync("/v1/dashboard/expiring-documents")).StatusCode);
        var recruiter = await As(UserRole.Recruiter);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/v1/dashboard/expiring-documents")).StatusCode);
    }

    private static PersonDocument Doc(Guid personId, Guid typeId, DateOnly expires, DocumentStatus status = DocumentStatus.Delivered, Guid? supersededBy = null) =>
        new()
        {
            PersonId = personId,
            DocumentTypeId = typeId,
            Status = status,
            StorageKey = $"k/{Guid.NewGuid():N}",
            OriginalFilename = "f.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            ExpiresAt = expires,
            SupersededBy = supersededBy,
            RejectionReason = status == DocumentStatus.Rejected ? "Ilegible" : null,
        };
}
