using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>Grupos de nómina, puestos with rate history and group concepts (SPEC §5.6, DATA_MODEL §5.1–5.3, PLAN E12).</summary>
public sealed class PayrollGroupTests(ApiFactory factory) : ApiTest(factory)
{
    public static object NewGroup(Guid clientId, string name = "Validadores 12 hrs") => new
    {
        clientId,
        name,
        periodType = "weekly",
        cutoffWeekday = 4, // jueves: SEM 38 runs viernes–jueves
        shiftHours = "12",
        attendanceLockTime = "23:59:00",
        seventhDayDivisor = 6,
        sundayPremiumPct = "0.25",
        holidayWorkedMultiplier = "2",
        scheduleDescription = "Lunes a domingo 12 hrs corte viernes a jueves",
        timeZone = (string?)null,
        isActive = true,
    };

    public static object NewPosition(string name = "Validador", string pocket = "428.57", string? validFrom = null) => new
    {
        name,
        dailySalaryPocket = pocket,
        dailySalaryImss = "278.80",
        hourlyRate = "35.7142",
        daysWorkedBonus = "450",
        validFrom,
    };

    private static decimal Dec(JsonElement e, string name) => decimal.Parse(e.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task A_group_with_its_puestos_and_rates_makes_the_client_a_payroll_client()
    {
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var owner = await AsOwner();

        var group = await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(ecovia.Uid)));
        Assert.Equal("VALIDADORES 12 HRS", group.GetProperty("name").GetString());
        Assert.Equal("America/Monterrey", group.GetProperty("timeZone").GetString());
        Assert.True(await WithDb(db => db.Set<Client>().Where(c => c.Uid == ecovia.Uid).Select(c => c.UsesPayroll).SingleAsync()));

        var id = group.GetProperty("id").GetGuid();
        var withPosition = await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{id}/positions", NewPosition()));
        var position = withPosition.GetProperty("positions").EnumerateArray().Single();
        Assert.Equal("VALIDADOR", position.GetProperty("name").GetString());
        Assert.Equal("428.57", position.GetProperty("current").GetProperty("dailySalaryPocket").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/v1/payroll/groups/{id}/positions", NewPosition("validador"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(ecovia.Uid, "validadores 12 HRS"))).StatusCode);
        var list = await BodyOf(await owner.GetAsync($"/v1/payroll/groups?clientId={ecovia.Uid}"));
        Assert.Equal(1, list.EnumerateArray().Single().GetProperty("positions").GetInt32());
    }

    [Fact]
    public async Task New_rates_close_the_previous_ones_and_keep_the_history()
    {
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var owner = await AsOwner();
        var today = new SystemClock().Today();
        var id = (await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(ecovia.Uid)))).GetProperty("id").GetGuid();
        var group = await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{id}/positions", NewPosition(validFrom: Day(today.AddDays(-30)))));
        var positionId = group.GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();

        var raise = new { dailySalaryPocket = "450.00", dailySalaryImss = "278.80", hourlyRate = "37.5", daysWorkedBonus = "450", validFrom = Day(today.AddDays(7)) };
        var after = await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/positions/{positionId}/rates", raise));
        var position = after.GetProperty("positions").EnumerateArray().Single();
        Assert.Equal("428.57", position.GetProperty("current").GetProperty("dailySalaryPocket").GetString()); // the raise starts next week
        var history = position.GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(["450.00", "428.57"], history.Select(h => h.GetProperty("dailySalaryPocket").GetString()));
        Assert.Equal(Day(today.AddDays(6)), history[1].GetProperty("validTo").GetString());

        var backwards = await owner.PostAsJsonAsync($"/v1/payroll/positions/{positionId}/rates", raise with { validFrom = Day(today.AddDays(-60)) });
        Assert.Contains("validFrom", (await ErrorsOf(backwards)).Keys);
        Assert.Contains("hourlyRate", (await ErrorsOf(await owner.PostAsJsonAsync($"/v1/payroll/positions/{positionId}/rates",
            raise with { hourlyRate = "35.71428" }))).Keys);
    }

    [Fact]
    public async Task Bonuses_start_inactive_until_the_client_defines_its_scheme_and_can_be_set_per_group()
    {
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var owner = await AsOwner();
        var group = await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(ecovia.Uid)));
        var concepts = group.GetProperty("concepts").EnumerateArray().ToList();
        Assert.Equal(["BONUS_DAYS_WORKED", "BONUS_PRODUCTIVITY", "BONUS_PUNCTUALITY", "REST_DAY_WORKED"],
            concepts.Select(c => c.GetProperty("code").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(concepts, c => Assert.False(c.GetProperty("isActive").GetBoolean()));
        var punctuality = concepts.Single(c => c.GetProperty("code").GetString() == "BONUS_PUNCTUALITY");
        Assert.Equal((400m, 7m), (Dec(punctuality, "amount"), Dec(punctuality, "daysBasis")));
        Assert.True(concepts.Single(c => c.GetProperty("code").GetString() == "BONUS_DAYS_WORKED").GetProperty("amountFromPosition").GetBoolean());

        var id = group.GetProperty("id").GetGuid();
        var updated = await BodyOf(await owner.PutAsJsonAsync($"/v1/payroll/groups/{id}/concepts", new
        {
            concepts = new[] { new { code = "BONUS_PUNCTUALITY", isActive = true, amount = (string?)"420", daysBasis = (string?)"7" } },
        }));
        var on = updated.GetProperty("concepts").EnumerateArray().Single(c => c.GetProperty("code").GetString() == "BONUS_PUNCTUALITY");
        Assert.True(on.GetProperty("isActive").GetBoolean());
        Assert.Equal(420m, Dec(on, "amount"));

        var salary = await owner.PutAsJsonAsync($"/v1/payroll/groups/{id}/concepts", new
        {
            concepts = new[] { new { code = "SALARY", isActive = false, amount = (string?)null, daysBasis = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, salary.StatusCode);
    }

    [Fact]
    public async Task The_pay_concepts_are_the_engines_with_the_same_excel_columns()
    {
        var rows = await WithDb(db => db.Set<PayConceptRow>().OrderBy(c => c.SortOrder).ToListAsync());
        Assert.Equal(PayConcepts.All.Select(c => c.Code), rows.Select(r => r.Code));
        foreach (var info in PayConcepts.All)
            Assert.Equal(info.ExportColumn.Length == 0 ? null : info.ExportColumn, rows.Single(r => r.Code == info.Code).ExportColumn);
        Assert.Equal(PayConcepts.All.Select(c => c.Kind), rows.Select(r => r.Kind));
    }

    [Fact]
    public async Task A_client_scoped_prenomina_grant_reaches_only_the_groups_of_its_clients()
    {
        var (ecovia, heb) = await WithDb(async db => (await db.AddClient("ECOVIA"), await db.AddClient("HEB")));
        var owner = await AsOwner();
        var hebGroup = (await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(heb.Uid, "Limpieza")))).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync("/v1/payroll/groups", NewGroup(ecovia.Uid));
        var (_, scoped) = await SignedIn(UserRole.Administrator,
            new UserPermission { Module = PermissionModule.PayrollPrepayroll, ClientId = ecovia.Uid });

        Assert.Equal(["ECOVIA"], (await BodyOf(await scoped.GetAsync("/v1/payroll/groups"))).EnumerateArray()
            .Select(g => g.GetProperty("clientName").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.GetAsync($"/v1/payroll/groups/{hebGroup}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.PostAsJsonAsync("/v1/payroll/groups", NewGroup(heb.Uid, "Otro"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.PostAsJsonAsync($"/v1/payroll/groups/{hebGroup}/positions", NewPosition())).StatusCode);

        var staffOnly = await As(UserRole.Administrator, PermissionModule.PayrollStaff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffOnly.GetAsync("/v1/payroll/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Recruiter)).GetAsync("/v1/payroll/groups")).StatusCode);
    }
}
