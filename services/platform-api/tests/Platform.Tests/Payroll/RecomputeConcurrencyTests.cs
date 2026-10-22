using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>
/// Found by the 500-employee load run (S12 hardening, D-013): concurrent writes for one person and period recomputed the same
/// prenómina line at once and the second failed (500). Each recompute now waits for the other; every write lands.
/// </summary>
public sealed class RecomputeConcurrencyTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task A_week_captured_at_once_for_one_person_lands_every_day_in_the_line()
    {
        var owner = await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions")[0].GetProperty("id").GetGuid();
        var employee = await WithDb(async db =>
        {
            var p = await db.AddPerson();
            var e = new Employee { PersonId = p.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = new DateOnly(2026, 1, 1) };
            db.Add(e);
            await db.SaveChangesAsync();
            return e.Uid;
        });
        var period = (await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();

        var days = Enumerable.Range(11, 7).Select(d => new DateOnly(2026, 9, d)).ToList();
        var writes = await Task.WhenAll(days.Select(d => owner.PostAsJsonAsync("/v1/attendance/unlock", new
        {
            employeeId = employee,
            date = d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            siteId = (Guid?)null,
            status = d.Day == 13 ? "rest_day" : "present",
            hoursWorked = (string?)null,
            overtimeHours = (string?)null,
            note = (string?)null,
            reason = "Carga",
        })));
        Assert.All(writes, w => Assert.Equal(HttpStatusCode.OK, w.StatusCode));

        var grid = await BodyOf(await owner.GetAsync($"/v1/payroll/periods/{period}"));
        var row = grid.GetProperty("rows")[0];
        Assert.Equal(6m, row.GetProperty("daysWorked").ValueKind == JsonValueKind.Number ? row.GetProperty("daysWorked").GetDecimal()
            : decimal.Parse(row.GetProperty("daysWorked").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Group_edits_recomputed_while_days_are_captured_neither_lose_a_day_nor_fail()
    {
        var owner = await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions")[0].GetProperty("id").GetGuid();
        var employee = await WithDb(async db =>
        {
            var p = await db.AddPerson();
            var e = new Employee { PersonId = p.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = new DateOnly(2026, 1, 1) };
            db.Add(e);
            await db.SaveChangesAsync();
            return e.Uid;
        });
        var period = (await BodyOf(await owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();

        var days = Enumerable.Range(11, 7).Select(d => new DateOnly(2026, 9, d)).ToList();
        var writes = days.Select(d => owner.PostAsJsonAsync("/v1/attendance/unlock", new
        {
            employeeId = employee,
            date = d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            siteId = (Guid?)null,
            status = d.Day == 13 ? "rest_day" : "present",
            hoursWorked = (string?)null,
            overtimeHours = (string?)null,
            note = (string?)null,
            reason = "Carga",
        })).ToList();
        // Rate changes one after another (they close and open the same row), all while the days are being captured.
        var rates = Task.Run(async () =>
        {
            var results = new List<HttpResponseMessage>();
            for (var i = 1; i <= 3; i++)
                results.Add(await owner.PostAsJsonAsync($"/v1/payroll/positions/{position}/rates", new
                {
                    dailySalaryPocket = (400 + i).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    dailySalaryImss = "300",
                    hourlyRate = "50",
                    daysWorkedBonus = "0",
                    validFrom = $"2021-0{i}-01",
                }));
            return results;
        });
        var all = (await Task.WhenAll(writes)).Concat(await rates).ToList();
        Assert.All(all, w => Assert.Equal(HttpStatusCode.OK, w.StatusCode));

        var row = (await BodyOf(await owner.GetAsync($"/v1/payroll/periods/{period}"))).GetProperty("rows")[0];
        var worked = row.GetProperty("daysWorked");
        Assert.Equal(6m, worked.ValueKind == JsonValueKind.Number ? worked.GetDecimal()
            : decimal.Parse(worked.GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }
}
