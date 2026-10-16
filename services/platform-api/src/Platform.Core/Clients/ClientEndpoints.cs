using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Core.Clients;

/// <summary>
/// Clients, contacts and sites (SPEC §3.3, §4.5, §5.6). Two entries create clients without asking a module:
/// <c>POST /v1/clients</c> (Reclutamiento → Clientes) marks <c>uses_recruiting</c>, <c>POST /v1/payroll/clients</c>
/// (Nómina → Clientes (nómina), grant <c>payroll_prepayroll</c>) marks <c>uses_payroll</c>. The payroll entry honours the
/// grant's client scope; the recruiting side needs <c>recruiting</c> (PLAN D69). <c>GET /v1/clients/options</c> is the
/// client picker of any Administrador (Usuarios, scoped grants): id and short name only.
/// </summary>
public static class ClientEndpoints
{
    public const string Tag = "clients";

    public const string ShortNameTaken = "Ya existe un cliente con ese nombre corto.";
    public const string NotFound = "El cliente no existe.";
    public const string ScopedCannotCreate =
        "Tu permiso de Prenómina está limitado a ciertos clientes; pide al administrador que dé de alta el cliente.";

    private static readonly AccessRule Admin = AccessRule.Administrator(PermissionModule.Recruiting);
    private static readonly AccessRule Payroll = AccessRule.Administrator(PermissionModule.PayrollPrepayroll);

    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder v1)
    {
        var clients = v1.MapGroup("/clients").WithTags("Clients");
        var payroll = v1.MapGroup("/payroll/clients").WithTags("Clients");

        clients.MapGet("", (DbContext db, CancellationToken ct) => List(db, null, ct))
            .WithName("ListClients").Produces<List<ClientListItemDto>>().RequireAccess(Admin);
        payroll.MapGet("", (DbContext db, RequestContext ctx, CancellationToken ct) =>
                List(db, ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollPrepayroll), ct))
            .WithName("ListPayrollClients").Produces<List<ClientListItemDto>>().RequireAccess(Payroll);

        clients.MapGet("/options", async (DbContext db, CancellationToken ct) => TypedResults.Ok(await db.Set<Client>().AsNoTracking()
                .OrderBy(c => c.ShortName).Select(c => new ClientOptionDto(c.Uid, c.ShortName, c.IsActive)).ToListAsync(ct)))
            .WithName("ListClientOptions").RequireAccess(AccessRule.Administrator());

        clients.MapGet("/{id:guid}", (Guid id, DbContext db, CancellationToken ct) => Detail(db, id, ct))
            .WithName("GetClient").Produces<ClientDetailDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Admin);
        payroll.MapGet("/{id:guid}", (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
                InScope(ctx, id) ? Detail(db, id, ct) : Task.FromResult(ApiResults.NotFound(NotFound)))
            .WithName("GetPayrollClient").Produces<ClientDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(Payroll);

        clients.MapPost("", (CreateClientRequest r, DbContext db, CancellationToken ct) => Create(db, r, recruiting: true, ct))
            .WithName("CreateClient").Mutation<CreateClientRequest>().RequireAccess(Admin);
        payroll.MapPost("", (CreateClientRequest r, DbContext db, RequestContext ctx, CancellationToken ct) =>
                ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollPrepayroll) is not null
                    ? Task.FromResult(ApiResults.Forbidden(ScopedCannotCreate))
                    : Create(db, r, recruiting: false, ct))
            .WithName("CreatePayrollClient").Mutation<CreateClientRequest>().RequireAccess(Payroll);

        clients.MapPut("/{id:guid}", async (Guid id, UpdateClientRequest r, DbContext db, CancellationToken ct) =>
            {
                if (await db.Set<Client>().SingleOrDefaultAsync(c => c.Uid == id, ct) is not { } c)
                    return ApiResults.NotFound(NotFound);
                c.ShortName = ShortName(r.ShortName);
                c.LegalName = r.LegalName.Clean();
                c.Rfc = ClientRules.Normalize(r.Rfc);
                c.Notes = r.Notes.Clean();
                c.IsActive = r.IsActive;
                return await db.SaveOrConflictAsync(() => TypedResults.NoContent(), ShortNameTaken, ct);
            })
            .WithName("UpdateClient").Mutation<UpdateClientRequest>().RequireAccess(Admin);

        MapContacts(clients);
        MapSites(clients);
        return v1;
    }

    private static RouteHandlerBuilder Mutation<TBody>(this RouteHandlerBuilder route) where TBody : class =>
        route.Validate<TBody>().Produces<ClientDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag);

    private static bool InScope(RequestContext ctx, Guid clientId) =>
        ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollPrepayroll) is not { } scope || scope.Contains(clientId);

    /// <summary>The short name is what the owner reads everywhere ("ECOVIA", "HEB"): trimmed, uppercase.</summary>
    private static string ShortName(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static async Task<IResult> List(DbContext db, IReadOnlySet<Guid>? scope, CancellationToken ct)
    {
        var query = db.Set<Client>().AsNoTracking();
        if (scope is not null)
            query = query.Where(c => scope.Contains(c.Uid));
        var rows = await query.OrderBy(c => c.ShortName)
            .Select(c => new
            {
                Client = c,
                Primary = c.Contacts.FirstOrDefault(x => x.IsPrimary),
                Zone = c.Sites.Where(s => s.IsActive && s.ZoneId != null).OrderBy(s => s.CreatedAt)
                    .Join(db.Set<Zone>(), s => s.ZoneId, z => z.Uid, (s, z) => z.NameEs).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return TypedResults.Ok(rows.Select(x => new ClientListItemDto(x.Client.Uid, x.Client.ShortName, x.Client.LegalName,
            x.Client.Rfc, x.Client.UsesRecruiting, x.Client.UsesPayroll, x.Client.IsActive, x.Zone,
            x.Primary is null ? null : ContactDto.From(x.Primary))).ToList());
    }

    private static async Task<IResult> Detail(DbContext db, Guid id, CancellationToken ct) =>
        await LoadDetail(db, id, ct) is { } dto ? TypedResults.Ok(dto) : ApiResults.NotFound(NotFound);

    public static async Task<ClientDetailDto?> LoadDetail(DbContext db, Guid id, CancellationToken ct)
    {
        var c = await db.Set<Client>().AsNoTracking().Include(x => x.Contacts).Include(x => x.Sites)
            .SingleOrDefaultAsync(x => x.Uid == id, ct);
        if (c is null)
            return null;
        var zoneIds = c.Sites.Where(s => s.ZoneId != null).Select(s => s.ZoneId!.Value).ToList();
        var zones = await db.Set<Zone>().Where(z => zoneIds.Contains(z.Uid)).ToDictionaryAsync(z => z.Uid, z => z.NameEs, ct);
        return new ClientDetailDto(c.Uid, c.ShortName, c.LegalName, c.Rfc, c.Notes, c.UsesRecruiting, c.UsesPayroll, c.IsActive,
            c.Contacts.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).Select(ContactDto.From).ToList(),
            c.Sites.OrderBy(s => s.Name).Select(s => new SiteDto(s.Uid, s.Name, s.ZoneId,
                s.ZoneId is { } z ? zones.GetValueOrDefault(z) : null, s.Street, s.ExtNumber, s.Neighborhood, s.PostalCode,
                s.Municipality, s.State, s.IsActive)).ToList());
    }

    private static async Task<IResult> Create(DbContext db, CreateClientRequest r, bool recruiting, CancellationToken ct)
    {
        var client = new Client
        {
            ShortName = ShortName(r.ShortName),
            LegalName = r.LegalName.Clean(),
            Rfc = ClientRules.Normalize(r.Rfc),
            UsesRecruiting = recruiting,
            UsesPayroll = !recruiting,
        };
        if (r.PrimaryContact is { } p)
        {
            client.Contacts.Add(new ClientContact
            {
                FullName = p.FullName.Trim(),
                Email = p.Email.Clean(),
                Phone = p.Phone.Clean(),
                JobTitle = "Contacto principal",
                IsPrimary = true,
            });
        }
        db.Add(client);
        return await db.SaveOrConflictAsync(async () => TypedResults.Ok(await LoadDetail(db, client.Uid, ct)), ShortNameTaken, ct);
    }

    private static void MapContacts(RouteGroupBuilder clients)
    {
        clients.MapPost("/{id:guid}/contacts", async (Guid id, ContactRequest r, DbContext db, CancellationToken ct) =>
            {
                if (!await db.Set<Client>().AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(NotFound);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (r.IsPrimary)
                    await ClearPrimary(db, id, null, ct);
                db.Add(new ClientContact
                {
                    ClientId = id,
                    FullName = r.FullName.Trim(),
                    Email = r.Email.Clean(),
                    Phone = r.Phone.Clean(),
                    JobTitle = r.JobTitle.Clean(),
                    IsPrimary = r.IsPrimary,
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(await LoadDetail(db, id, ct));
            })
            .WithName("CreateClientContact").Mutation<ContactRequest>().RequireAccess(Admin);

        clients.MapPut("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ContactRequest r, DbContext db,
                CancellationToken ct) =>
            {
                if (await db.Set<ClientContact>().SingleOrDefaultAsync(c => c.Uid == contactId && c.ClientId == id, ct) is not { } c)
                    return ApiResults.NotFound("El contacto no existe.");
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (r.IsPrimary && !c.IsPrimary)
                    await ClearPrimary(db, id, contactId, ct);
                (c.FullName, c.Email, c.Phone, c.JobTitle, c.IsPrimary) =
                    (r.FullName.Trim(), r.Email.Clean(), r.Phone.Clean(), r.JobTitle.Clean(), r.IsPrimary);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(await LoadDetail(db, id, ct));
            })
            .WithName("UpdateClientContact").Mutation<ContactRequest>().RequireAccess(Admin);

        clients.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, DbContext db, CancellationToken ct) =>
            {
                if (await db.Set<ClientContact>().SingleOrDefaultAsync(c => c.Uid == contactId && c.ClientId == id, ct) is not { } c)
                    return ApiResults.NotFound("El contacto no existe.");
                if (c.UserId is not null)
                    return ApiResults.Conflict("El contacto tiene acceso al portal; desactiva su usuario en Usuarios antes de eliminarlo.");
                db.Remove(c);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await LoadDetail(db, id, ct));
            })
            .WithName("DeleteClientContact").Produces<ClientDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag).RequireAccess(Admin);
    }

    /// <summary>The previous primary contact loses the flag first (one primary per client, partial unique index).</summary>
    private static async Task ClearPrimary(DbContext db, Guid clientId, Guid? except, CancellationToken ct)
    {
        var current = await db.Set<ClientContact>().Where(c => c.ClientId == clientId && c.IsPrimary && c.Uid != except).ToListAsync(ct);
        current.ForEach(c => c.IsPrimary = false);
        await db.SaveChangesAsync(ct);
    }

    private static void MapSites(RouteGroupBuilder clients)
    {
        const string conflict = "Este cliente ya tiene un sitio con ese nombre.";

        clients.MapPost("/{id:guid}/sites", async (Guid id, SiteRequest r, DbContext db, CancellationToken ct) =>
            {
                if (!await db.Set<Client>().AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(NotFound);
                if (await UnknownZone(db, r.ZoneId, ct) is { } bad)
                    return bad;
                var site = new ClientSite { ClientId = id, Name = r.Name.Trim() };
                Apply(site, r);
                db.Add(site);
                return await db.SaveOrConflictAsync(async () => TypedResults.Ok(await LoadDetail(db, id, ct)), conflict, ct);
            })
            .WithName("CreateClientSite").Mutation<SiteRequest>().RequireAccess(Admin);

        clients.MapPut("/{id:guid}/sites/{siteId:guid}", async (Guid id, Guid siteId, SiteRequest r, DbContext db, CancellationToken ct) =>
            {
                if (await db.Set<ClientSite>().SingleOrDefaultAsync(s => s.Uid == siteId && s.ClientId == id, ct) is not { } site)
                    return ApiResults.NotFound("El sitio no existe.");
                if (await UnknownZone(db, r.ZoneId, ct) is { } bad)
                    return bad;
                Apply(site, r);
                return await db.SaveOrConflictAsync(async () => TypedResults.Ok(await LoadDetail(db, id, ct)), conflict, ct);
            })
            .WithName("UpdateClientSite").Mutation<SiteRequest>().RequireAccess(Admin);
    }

    private static void Apply(ClientSite s, SiteRequest r)
    {
        s.Name = r.Name.Trim();
        s.ZoneId = r.ZoneId;
        s.Street = r.Street.Clean();
        s.ExtNumber = r.ExtNumber.Clean();
        s.Neighborhood = r.Neighborhood.Clean();
        s.PostalCode = r.PostalCode.Clean();
        s.Municipality = r.Municipality.Clean();
        s.State = r.State.Clean();
        s.IsActive = r.IsActive;
    }

    private static async Task<IResult?> UnknownZone(DbContext db, Guid? zoneId, CancellationToken ct) =>
        zoneId is { } z && !await db.Set<Zone>().AnyAsync(x => x.Uid == z, ct)
            ? ApiResults.Validation(new Dictionary<string, string[]> { ["zoneId"] = ["La zona no existe."] })
            : null;
}
