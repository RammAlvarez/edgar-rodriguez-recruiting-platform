using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Core.People;

/// <summary>
/// The people registry (SPEC §3.1). Search, duplicates and create are open to staff so «Nuevo candidato» can find or add a
/// person; reading or editing a profile is limited by <see cref="IPersonScope"/> (PLAN D73). Reading a profile is logged in
/// <c>data_access_log</c>.
/// </summary>
public static class PeopleEndpoints
{
    public const string Tag = "people";

    public const string NotFound = "La persona no existe.";

    /// <summary>Administradores and Reclutadores; profile reads and edits go through <see cref="IPersonScope"/> (D73).</summary>
    private static readonly AccessRule Staff = AccessRule.ForRoles(UserRole.Administrator, UserRole.Recruiter);

    public static IEndpointRouteBuilder MapPeopleEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/people").WithTags("People");

        g.MapGet("", async (string? search, DbContext db, CancellationToken ct) =>
            {
                var q = db.Set<Person>().AsNoTracking().Where(p => p.DeletedAt == null);
                if (search.Clean() is { } s)
                {
                    var id = PersonText.Id(s)!;
                    var name = PersonText.Name(s);
                    q = q.Where(p => p.Curp == id || p.Rfc == id || p.MobilePhone == s || EF.Functions.ILike(p.FullName, $"%{name}%"));
                }
                var rows = await q.OrderBy(p => p.FullName).Take(50)
                    .Select(p => new PersonSummaryDto(p.Uid, p.FullName, p.Curp, p.Rfc, p.MobilePhone, p.Email)).ToListAsync(ct);
                return TypedResults.Ok(rows);
            })
            .WithName("ListPeople").RequireAccess(Staff);

        g.MapGet("/duplicates", async (string? curp, string? rfc, Guid? excludeId, DbContext db, CancellationToken ct) =>
                TypedResults.Ok(await Duplicates(db, PersonText.Id(curp), PersonText.Id(rfc), excludeId, ct)))
            .WithName("FindDuplicatePeople").RequireAccess(Staff);

        g.MapGet("/{id:guid}", async (Guid id, DbContext db, DataAccess log, IPersonScope scope, CancellationToken ct) =>
            {
                if (!await scope.CanAccessAsync(id, ct))
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<Person>().AsNoTracking().SingleOrDefaultAsync(p => p.Uid == id && p.DeletedAt == null, ct) is not { } p)
                    return ApiResults.NotFound(NotFound);
                await log.RecordAsync(p.Uid, DataAccessKind.ViewProfile, null, ct);
                return TypedResults.Ok(PersonDto.From(p));
            })
            .WithName("GetPerson").Produces<PersonDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Staff);

        g.MapPost("", async (PersonRequest r, DbContext db, CancellationToken ct) =>
            {
                var person = new Person { FirstNames = "", LastNamePaternal = "" };
                Apply(person, r);
                if (await DuplicateConflict(db, person, ct) is { } conflict)
                    return conflict;
                db.Add(person);
                return await db.SaveOrConflictAsync(async () => TypedResults.Ok(await Reload(db, person.Uid, ct)),
                    "Ya existe una persona con esa CURP o RFC.", ct);
            })
            .WithName("CreatePerson").Validate<PersonRequest>().Produces<PersonDto>().ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAccess(Staff).Invalidates(Tag);

        g.MapPut("/{id:guid}", async (Guid id, PersonRequest r, DbContext db, IPersonScope scope, CancellationToken ct) =>
            {
                if (!await scope.CanAccessAsync(id, ct))
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<Person>().SingleOrDefaultAsync(p => p.Uid == id && p.DeletedAt == null, ct) is not { } person)
                    return ApiResults.NotFound(NotFound);
                Apply(person, r);
                if (await DuplicateConflict(db, person, ct) is { } conflict)
                    return conflict;
                return await db.SaveOrConflictAsync(async () => TypedResults.Ok(await Reload(db, person.Uid, ct)),
                    "Ya existe una persona con esa CURP o RFC.", ct);
            })
            .WithName("UpdatePerson").Validate<PersonRequest>().Produces<PersonDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(Staff).Invalidates(Tag);

        return v1;
    }

    /// <summary>Re-read so the generated <c>full_name</c> comes back.</summary>
    private static async Task<PersonDto> Reload(DbContext db, Guid id, CancellationToken ct) =>
        PersonDto.From(await db.Set<Person>().AsNoTracking().SingleAsync(p => p.Uid == id, ct));

    public static async Task<List<DuplicatePersonDto>> Duplicates(DbContext db, string? curp, string? rfc, Guid? excludeId,
        CancellationToken ct)
    {
        if (curp is null && rfc is null)
            return [];
        var matches = await db.Set<Person>().AsNoTracking()
            .Where(p => p.Uid != excludeId && ((curp != null && p.Curp == curp) || (rfc != null && p.Rfc == rfc)))
            .ToListAsync(ct);
        return matches.Select(p => new DuplicatePersonDto(p.Uid, p.FullName, p.Curp, p.Rfc, p.DeletedAt is not null,
            [.. new[] { curp is not null && p.Curp == curp ? "curp" : null, rfc is not null && p.Rfc == rfc ? "rfc" : null }
                .OfType<string>()])).ToList();
    }

    public static async Task<IResult?> DuplicateConflict(DbContext db, Person person, CancellationToken ct)
    {
        var duplicate = (await Duplicates(db, person.Curp, person.Rfc, person.Uid, ct)).FirstOrDefault();
        if (duplicate is null)
            return null;
        // «la CURP», «el RFC».
        var (article, demonstrative, field) = duplicate.MatchedOn.Contains("curp") ? ("La", "esa", "CURP") : ("El", "ese", "RFC");
        return ApiResults.Conflict(duplicate.IsDeleted
            ? $"{article} {field} pertenece a {duplicate.FullName}, que fue dado de baja del registro."
            : $"Ya existe una persona con {demonstrative} {field}: {duplicate.FullName}.");
    }

    public static void Apply(Person p, PersonRequest r)
    {
        p.FirstNames = PersonText.Name(r.FirstNames);
        p.LastNamePaternal = PersonText.Name(r.LastNamePaternal);
        p.LastNameMaternal = PersonText.OptionalName(r.LastNameMaternal);
        p.Curp = PersonText.Id(r.Curp);
        p.Rfc = PersonText.Id(r.Rfc);
        // The CURP carries the birth date and sex: fill them when not typed (a typed birth date must match, see validator).
        var fromCurp = p.Curp is { } c && MexicanIds.IsCurp(c) ? c : null;
        p.BirthDate = r.BirthDate ?? (fromCurp is null ? null : MexicanIds.CurpBirthDate(fromCurp));
        p.Gender = r.Gender ?? fromCurp?[10] switch
        {
            'H' => Gender.Male,
            'M' => Gender.Female,
            _ => Gender.Unspecified,
        };
        p.BirthState = PersonText.OptionalName(r.BirthState);
        p.IsForeign = r.IsForeign;
        p.Nss = r.Nss.Clean();
        p.MobilePhone = r.MobilePhone.Clean();
        p.Email = r.Email.Clean()?.ToLowerInvariant();
        p.Street = r.Street.Clean();
        p.ExtNumber = r.ExtNumber.Clean();
        p.Neighborhood = r.Neighborhood.Clean();
        p.PostalCode = r.PostalCode.Clean();
        p.Municipality = r.Municipality.Clean();
        p.State = r.State.Clean();
        p.Source = r.Source;
        p.Notes = r.Notes.Clean();
    }
}
