using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Core.Catalogs;

/// <summary>
/// SPEC §3.4 catalogs. Everyone on staff reads them (the forms of every screen); only Administradores edit them, and no
/// delegated grant covers them. Nothing is deleted except holidays: rows are deactivated so past records keep their labels.
/// </summary>
public static class CatalogEndpoints
{
    public const string Tag = "catalogs";

    private static readonly AccessRule Edit = AccessRule.Administrator();

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/catalogs").WithTags("Catalogs");

        g.MapGet("", async (DbContext db, CancellationToken ct) => TypedResults.Ok(new CatalogsDto(
                (await db.Set<Zone>().OrderBy(z => z.State).ThenBy(z => z.NameEs).ToListAsync(ct)).Select(ZoneDto.From).ToList(),
                (await db.Set<CatalogPosition>().OrderBy(p => p.NameEs).ToListAsync(ct)).Select(CatalogPositionDto.From).ToList(),
                (await db.Set<EducationLevel>().OrderBy(e => e.SortOrder).ToListAsync(ct)).Select(EducationLevelDto.From).ToList(),
                (await db.Set<Benefit>().OrderBy(b => b.NameEs).ToListAsync(ct)).Select(BenefitDto.From).ToList(),
                (await db.Set<Holiday>().OrderBy(h => h.Date).ToListAsync(ct)).Select(HolidayDto.From).ToList(),
                (await db.Set<Bank>().OrderBy(b => b.SpeiCode).ToListAsync(ct)).Select(BankDto.From).ToList(),
                (await db.Set<DocumentType>().OrderBy(d => d.SortOrder).ToListAsync(ct)).Select(DocumentTypeDto.From).ToList(),
                (await db.Set<DocumentTemplate>().Include(t => t.Items).OrderBy(t => t.NameEs).ToListAsync(ct))
                    .Select(DocumentTemplateDto.From).ToList())))
            .WithName("GetCatalogs")
            .RequireAccess(AccessRule.ForRoles(UserRole.Administrator, UserRole.Recruiter));

        MapZones(g);
        MapPositions(g);
        MapEducationLevels(g);
        MapBenefits(g);
        MapHolidays(g);
        MapBanks(g);
        MapDocumentTypes(g);
        MapDocumentTemplates(g);
        return v1;
    }

    private static RouteHandlerBuilder Mutation<TBody, TResult>(this RouteHandlerBuilder route, string name) where TBody : class =>
        route.WithName(name).Validate<TBody>().Produces<TResult>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(Edit).Invalidates(Tag);

    private static void MapZones(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe una zona con ese nombre en ese estado.";
        g.MapPost("/zones", (ZoneRequest r, DbContext db, CancellationToken ct) =>
        {
            var z = new Zone { NameEs = r.NameEs.Trim(), State = r.State.Trim(), IsActive = r.IsActive };
            db.Add(z);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(ZoneDto.From(z)), conflict, ct);
        }).Mutation<ZoneRequest, ZoneDto>("CreateZone");

        g.MapPut("/zones/{id:guid}", async (Guid id, ZoneRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<Zone>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } z)
                return ApiResults.NotFound("La zona no existe.");
            (z.NameEs, z.State, z.IsActive) = (r.NameEs.Trim(), r.State.Trim(), r.IsActive);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(ZoneDto.From(z)), conflict, ct);
        }).Mutation<ZoneRequest, ZoneDto>("UpdateZone");
    }

    private static void MapPositions(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe un puesto con ese nombre.";
        g.MapPost("/positions", (CatalogPositionRequest r, DbContext db, CancellationToken ct) =>
        {
            var p = new CatalogPosition { NameEs = r.NameEs.Trim(), IsActive = r.IsActive };
            db.Add(p);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(CatalogPositionDto.From(p)), conflict, ct);
        }).Mutation<CatalogPositionRequest, CatalogPositionDto>("CreateCatalogPosition");

        g.MapPut("/positions/{id:guid}", async (Guid id, CatalogPositionRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<CatalogPosition>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } p)
                return ApiResults.NotFound("El puesto no existe.");
            (p.NameEs, p.IsActive) = (r.NameEs.Trim(), r.IsActive);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(CatalogPositionDto.From(p)), conflict, ct);
        }).Mutation<CatalogPositionRequest, CatalogPositionDto>("UpdateCatalogPosition");
    }

    private static void MapEducationLevels(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe un nivel de estudios con ese nombre.";
        g.MapPost("/education-levels", (EducationLevelRequest r, DbContext db, CancellationToken ct) =>
        {
            var e = new EducationLevel { NameEs = r.NameEs.Trim(), SortOrder = r.SortOrder, IsActive = r.IsActive };
            db.Add(e);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(EducationLevelDto.From(e)), conflict, ct);
        }).Mutation<EducationLevelRequest, EducationLevelDto>("CreateEducationLevel");

        g.MapPut("/education-levels/{id:guid}", async (Guid id, EducationLevelRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<EducationLevel>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } e)
                return ApiResults.NotFound("El nivel de estudios no existe.");
            (e.NameEs, e.SortOrder, e.IsActive) = (r.NameEs.Trim(), r.SortOrder, r.IsActive);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(EducationLevelDto.From(e)), conflict, ct);
        }).Mutation<EducationLevelRequest, EducationLevelDto>("UpdateEducationLevel");
    }

    private static void MapBenefits(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe una prestación con ese nombre.";
        g.MapPost("/benefits", (BenefitRequest r, DbContext db, CancellationToken ct) =>
        {
            var b = new Benefit { NameEs = r.NameEs.Trim(), IsDefault = r.IsDefault, IsActive = r.IsActive };
            db.Add(b);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(BenefitDto.From(b)), conflict, ct);
        }).Mutation<BenefitRequest, BenefitDto>("CreateBenefit");

        g.MapPut("/benefits/{id:guid}", async (Guid id, BenefitRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<Benefit>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } b)
                return ApiResults.NotFound("La prestación no existe.");
            (b.NameEs, b.IsDefault, b.IsActive) = (r.NameEs.Trim(), r.IsDefault, r.IsActive);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(BenefitDto.From(b)), conflict, ct);
        }).Mutation<BenefitRequest, BenefitDto>("UpdateBenefit");
    }

    private static void MapHolidays(RouteGroupBuilder g)
    {
        const string conflict = "Ya hay un día festivo en esa fecha.";
        g.MapPost("/holidays", (HolidayRequest r, DbContext db, CancellationToken ct) =>
        {
            var h = new Holiday { Date = r.Date, NameEs = r.NameEs.Trim(), IsMandatory = r.IsMandatory };
            db.Add(h);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(HolidayDto.From(h)), conflict, ct);
        }).Mutation<HolidayRequest, HolidayDto>("CreateHoliday");

        g.MapPut("/holidays/{id:guid}", async (Guid id, HolidayRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<Holiday>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } h)
                return ApiResults.NotFound("El día festivo no existe.");
            (h.Date, h.NameEs, h.IsMandatory) = (r.Date, r.NameEs.Trim(), r.IsMandatory);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(HolidayDto.From(h)), conflict, ct);
        }).Mutation<HolidayRequest, HolidayDto>("UpdateHoliday");

        g.MapDelete("/holidays/{id:guid}", async (Guid id, DbContext db, CancellationToken ct) =>
            {
                if (await db.Set<Holiday>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } h)
                    return ApiResults.NotFound("El día festivo no existe.");
                db.Remove(h);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteHoliday").ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Edit).Invalidates(Tag);

        // Adds the LFT art. 74 days of a year that are not in the catalog yet; dates already present are kept as they are.
        g.MapPost("/holidays/statutory", async (StatutoryHolidaysRequest r, DbContext db, CancellationToken ct) =>
            {
                var statutory = StatutoryHolidays.For(r.Year);
                var dates = statutory.Select(h => h.Date).ToList();
                var existing = await db.Set<Holiday>().Where(h => dates.Contains(h.Date)).Select(h => h.Date).ToListAsync(ct);
                var added = statutory.Where(h => !existing.Contains(h.Date))
                    .Select(h => new Holiday { Date = h.Date, NameEs = h.NameEs, IsMandatory = true }).ToList();
                db.AddRange(added);
                await db.SaveChangesAsync(ct);
                var year = await db.Set<Holiday>().Where(h => h.Date.Year == r.Year).OrderBy(h => h.Date).ToListAsync(ct);
                return TypedResults.Ok(new StatutoryHolidaysResult(added.Count, year.Select(HolidayDto.From).ToList()));
            })
            .WithName("AddStatutoryHolidays").Validate<StatutoryHolidaysRequest>().RequireAccess(Edit).Invalidates(Tag);
    }

    private static void MapBanks(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe un banco con esa clave SPEI.";
        g.MapPost("/banks", (BankRequest r, DbContext db, CancellationToken ct) =>
        {
            var b = new Bank
            {
                Name = r.Name.Trim(),
                SpeiCode = r.SpeiCode,
                SupportsRegistration = r.SupportsRegistration,
                SupportsMassivePayment = r.SupportsMassivePayment,
                IsActive = r.IsActive,
            };
            db.Add(b);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(BankDto.From(b)), conflict, ct);
        }).Mutation<BankRequest, BankDto>("CreateBank");

        g.MapPut("/banks/{id:guid}", async (Guid id, BankRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<Bank>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } b)
                return ApiResults.NotFound("El banco no existe.");
            (b.Name, b.SpeiCode, b.SupportsRegistration, b.SupportsMassivePayment, b.IsActive) =
                (r.Name.Trim(), r.SpeiCode, r.SupportsRegistration, r.SupportsMassivePayment, r.IsActive);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(BankDto.From(b)), conflict, ct);
        }).Mutation<BankRequest, BankDto>("UpdateBank");
    }

    private static void MapDocumentTypes(RouteGroupBuilder g)
    {
        g.MapPost("/document-types", (DocumentTypeRequest r, DbContext db, CancellationToken ct) =>
        {
            var d = new DocumentType
            {
                Code = r.Code,
                NameEs = r.NameEs.Trim(),
                HasExpiry = r.HasExpiry,
                DefaultValidityDays = r.DefaultValidityDays,
                SortOrder = r.SortOrder,
                IsActive = r.IsActive,
            };
            db.Add(d);
            return db.SaveOrConflictAsync(() => TypedResults.Ok(DocumentTypeDto.From(d)), "Ya existe un tipo de documento con esa clave.", ct);
        }).Mutation<DocumentTypeRequest, DocumentTypeDto>("CreateDocumentType");

        g.MapPut("/document-types/{id:guid}", async (Guid id, DocumentTypeRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<DocumentType>().SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } d)
                return ApiResults.NotFound("El tipo de documento no existe.");
            (d.NameEs, d.HasExpiry, d.DefaultValidityDays, d.SortOrder, d.IsActive) =
                (r.NameEs.Trim(), r.HasExpiry, r.DefaultValidityDays, r.SortOrder, r.IsActive);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(DocumentTypeDto.From(d));
        }).Mutation<DocumentTypeRequest, DocumentTypeDto>("UpdateDocumentType");
    }

    private static void MapDocumentTemplates(RouteGroupBuilder g)
    {
        const string conflict = "Ya existe una plantilla con ese nombre.";
        g.MapPost("/document-templates", async (DocumentTemplateRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await UnknownTypes(db, r, ct) is { } unknown)
                return unknown;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var t = new DocumentTemplate { NameEs = r.NameEs.Trim(), Scope = r.Scope };
            await ApplyDefault(db, t, r.IsDefault, ct);
            t.Items = r.Items.Select(i => new DocumentTemplateItem { DocumentTypeId = i.DocumentTypeId, IsRequired = i.IsRequired }).ToList();
            db.Add(t);
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(DocumentTemplateDto.From(t)), conflict, ct, tx);
        }).Mutation<DocumentTemplateRequest, DocumentTemplateDto>("CreateDocumentTemplate");

        g.MapPut("/document-templates/{id:guid}", async (Guid id, DocumentTemplateRequest r, DbContext db, CancellationToken ct) =>
        {
            if (await db.Set<DocumentTemplate>().Include(x => x.Items).SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } t)
                return ApiResults.NotFound("La plantilla no existe.");
            if (await UnknownTypes(db, r, ct) is { } unknown)
                return unknown;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            (t.NameEs, t.Scope) = (r.NameEs.Trim(), r.Scope);
            await ApplyDefault(db, t, r.IsDefault, ct);
            // Items are matched by document type so unchanged rows keep their uid and audit history.
            var wanted = r.Items.ToDictionary(i => i.DocumentTypeId);
            t.Items.RemoveAll(i => !wanted.ContainsKey(i.DocumentTypeId));
            foreach (var item in r.Items)
            {
                if (t.Items.Find(i => i.DocumentTypeId == item.DocumentTypeId) is { } current)
                    current.IsRequired = item.IsRequired;
                else
                    t.Items.Add(new DocumentTemplateItem { DocumentTypeId = item.DocumentTypeId, IsRequired = item.IsRequired });
            }
            return await db.SaveOrConflictAsync(() => TypedResults.Ok(DocumentTemplateDto.From(t)), conflict, ct, tx);
        }).Mutation<DocumentTemplateRequest, DocumentTemplateDto>("UpdateDocumentTemplate");
    }

    private static async Task<IResult?> UnknownTypes(DbContext db, DocumentTemplateRequest r, CancellationToken ct)
    {
        var ids = r.Items.Select(i => i.DocumentTypeId).ToList();
        var known = await db.Set<DocumentType>().CountAsync(d => ids.Contains(d.Uid), ct);
        return known == ids.Count
            ? null
            : ApiResults.Validation(new Dictionary<string, string[]> { ["items"] = ["Uno de los documentos no existe."] });
    }

    /// <summary>Making a template the default of its scope takes the flag from the previous one (same transaction).</summary>
    private static async Task ApplyDefault(DbContext db, DocumentTemplate t, bool isDefault, CancellationToken ct)
    {
        if (isDefault && !t.IsDefault)
        {
            var previous = await db.Set<DocumentTemplate>().Where(x => x.Scope == t.Scope && x.IsDefault && x.Uid != t.Uid).ToListAsync(ct);
            previous.ForEach(p => p.IsDefault = false);
            if (previous.Count > 0)
                await db.SaveChangesAsync(ct);
        }
        t.IsDefault = isDefault;
    }
}
