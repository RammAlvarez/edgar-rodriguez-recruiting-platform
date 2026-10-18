using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Tests.Payroll;
using Platform.Tests.Support;

namespace Platform.Tests.Reports;

/// <summary>
/// Reportes de nómina (S11, SPEC §5.9, PLAN D87): ECOVIA with four people over Semanas 38 and 39 — Ana and Beto every day, Carla
/// with two faltas and no account, Dora hired on the 15th and dada de baja after a week — and each report checked against the
/// prenómina grid and the payment files.
/// </summary>
public sealed class PayrollReportTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;

    private sealed record Arranged(Guid Client, Guid Group, Guid Week38, Guid Week39, Dictionary<string, Guid> Employees);

    private static decimal Dec(JsonElement e) => e.ValueKind == JsonValueKind.Number
        ? e.GetDecimal()
        : decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    private async Task<Arranged> ArrangeAsync()
    {
        _owner ??= await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions",
                PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var employees = new Dictionary<string, Guid>();
        await WithDb(async db =>
        {
            foreach (var (name, hired, missing) in new[]
                     {
                         ("ANA", new DateOnly(2026, 1, 1), Array.Empty<int>()), ("BETO", new DateOnly(2026, 1, 1), []),
                         ("CARLA", new DateOnly(2026, 1, 1), [12, 19]), ("DORA", new DateOnly(2026, 9, 15), []),
                     })
            {
                var person = await db.AddPerson(name, "PÉREZ", "LÓPEZ");
                var employee = new Employee { PersonId = person.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = hired };
                db.Add(employee);
                await db.SaveChangesAsync();
                for (var d = new DateOnly(2026, 9, 11); d <= new DateOnly(2026, 9, 24); d = d.AddDays(1))
                {
                    if (d < hired || missing.Contains(d.Day))
                        continue;
                    var rest = d.DayOfWeek == DayOfWeek.Sunday;
                    db.Add(new AttendanceRecord
                    {
                        EmployeeId = employee.Uid,
                        WorkDate = d,
                        Status = rest ? AttendanceStatus.RestDay : AttendanceStatus.Present,
                        HoursWorked = rest ? null : 12,
                        OvertimeHours = name == "BETO" && d.Day == 18 ? 2 : 0,
                        IsLocked = true,
                        RecordedAt = DateTimeOffset.UnixEpoch,
                    });
                }
                await db.SaveChangesAsync();
                employees[name] = employee.Uid;
            }
        });
        var week38 = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();
        var week39 = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-21" }))).GetProperty("id").GetGuid();
        foreach (var w in new[] { week38, week39 })
            Assert.Equal(HttpStatusCode.OK, (await _owner.PostAsync($"/v1/payroll/periods/{w}/close-capture", null)).StatusCode);
        return new Arranged(client.Uid, group, week38, week39, employees);
    }

    private async Task<JsonElement> Report(string report, string query = "") =>
        await BodyOf(await _owner.GetAsync($"/v1/payroll-reports/{report}{query}"));

    /// <summary>The column <paramref name="column"/> of a table, as the cells' numeric values (null when empty).</summary>
    private static decimal? Value(JsonElement table, JsonElement row, string column)
    {
        var i = table.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ToList().IndexOf(column);
        var v = row.GetProperty("cells")[i].GetProperty("value");
        return v.ValueKind == JsonValueKind.Null ? null : Dec(v);
    }

    private static string Text(JsonElement table, JsonElement row, string column)
    {
        var i = table.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ToList().IndexOf(column);
        return row.GetProperty("cells")[i].GetProperty("display").GetString()!;
    }

    private static List<JsonElement> Rows(JsonElement table) => table.GetProperty("rows").EnumerateArray().ToList();

    [Fact]
    public async Task Costo_de_nomina_is_the_grid_of_each_period_with_the_change_from_the_one_before()
    {
        var a = await ArrangeAsync();
        var report = await Report("cost", $"?clientId={a.Client}&from=2026-09-11&to=2026-09-24");
        var table = report.GetProperty("tables")[0];
        var rows = Rows(table);
        Assert.Equal(3, rows.Count);
        Assert.True(rows[2].GetProperty("emphasis").GetBoolean());

        var grids = new List<JsonElement>();
        foreach (var (row, period) in rows.Take(2).Zip([a.Week38, a.Week39]))
        {
            Assert.Equal(period, row.GetProperty("periodId").GetGuid());
            var grid = await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{period}"));
            grids.Add(grid);
            var footers = grid.GetProperty("columns").EnumerateArray().Select(c => c.GetString()!)
                .Zip(grid.GetProperty("footers").EnumerateArray().Select(Dec)).ToDictionary(x => x.First, x => x.Second);
            Assert.Equal(Dec(grid.GetProperty("totalNet")), Value(table, row, "Total neto"));
            Assert.Equal(footers["SUELDO"], Value(table, row, "Sueldo"));
            Assert.Equal(footers["HORAS EXTRA"], Value(table, row, "Horas extra"));
            Assert.Equal(grid.GetProperty("rows").EnumerateArray().Count(r => Dec(r.GetProperty("totalNet")) > 0), (int)Value(table, row, "Empleados pagados")!);
        }
        var (t38, t39) = (Dec(grids[0].GetProperty("totalNet")), Dec(grids[1].GetProperty("totalNet")));
        Assert.Null(Value(table, rows[0], "Δ $"));
        Assert.Equal(t39 - t38, Value(table, rows[1], "Δ $"));
        Assert.Equal(Math.Truncate((t39 - t38) / t38 * 100) / 100, Value(table, rows[1], "Δ %"));
        Assert.Equal(t38 + t39, Value(table, rows[2], "Total neto"));

        // Semana by default: the latest period (none is paid yet), Δ against the one before it.
        var week = await Report("cost");
        Assert.Equal(a.Week39, week.GetProperty("filters").GetProperty("periodId").GetGuid());
        Assert.Equal(t39 - t38, Value(week.GetProperty("tables")[0], Rows(week.GetProperty("tables")[0])[0], "Δ $"));

        // The export writes the same cells.
        var file = await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll-reports/exports",
            new { report = "cost", format = "xlsx", clientId = a.Client, periodId = (Guid?)null, from = "2026-09-11", to = "2026-09-24" }));
        Assert.StartsWith("Costo de nómina por cliente ECOVIA 2026-09-11 a 2026-09-24", file.GetProperty("fileName").GetString());
        var key = Factory.Storage.Contents.Keys.Single(k => k.Item2.StartsWith("payroll-reports/", StringComparison.Ordinal));
        using var book = new XLWorkbook(new MemoryStream(Factory.Storage.Contents[key]));
        Assert.Contains(book.Worksheet(1).CellsUsed(), c => c.DataType == XLDataType.Number && (decimal)c.GetDouble() == t38 + t39);
    }

    [Fact]
    public async Task Resumen_de_pago_follows_the_files_and_lists_who_is_still_unpaid()
    {
        var a = await ArrangeAsync();
        var bbva = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "012").Select(b => b.Uid).SingleAsync());
        var azteca = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "127").Select(b => b.Uid).SingleAsync());
        foreach (var (who, bank, type, number) in new[] { ("ANA", bbva, "clabe", "012180000000000015"), ("BETO", azteca, "own_account", "12345678901234") })
            await _owner.PostAsJsonAsync($"/v1/employees/{a.Employees[who]}/bank-accounts", new
            {
                bankId = bank,
                accountType = type,
                accountNumber = number,
                holderName = (string?)null,
                alias = (string?)null,
                validFrom = (string?)null,
                makePrimary = true,
            });
        await _owner.PutAsJsonAsync("/v1/payroll/paying-account", new
        {
            name = "Cuenta Azteca",
            chargeAccountNumber = "01234567890123",
            holderName = "MAXIMA REPARACION Y LIMPIEZA SA DE CV",
            holderRfc = "MRL2407307G7",
            chargeLegend = (string?)null,
            creditLegend = (string?)null,
        });
        var batch = (await BodyOf(await _owner.PostAsync($"/v1/payroll/periods/{a.Week38}/payment-files", null))).GetProperty("batch");
        await _owner.PostAsync($"/v1/payroll/payment-batches/{batch.GetProperty("id").GetGuid()}/uploaded", null);

        // Semana by default is now the paid one.
        var report = await Report("payments");
        Assert.Equal(a.Week38, report.GetProperty("filters").GetProperty("periodId").GetGuid());
        var summary = report.GetProperty("tables")[0];
        var byConcept = Rows(summary).ToDictionary(r => Text(summary, r, "Concepto"));
        var generated = Dec(batch.GetProperty("totalAmount"));
        Assert.Equal((2, generated), ((int)Value(summary, byConcept["Pagado por el banco"], "Personas")!, Value(summary, byConcept["Pagado por el banco"], "Importe")!.Value));
        var total = Value(summary, byConcept["Total neto del período"], "Importe")!.Value;
        Assert.Equal(total, generated + Value(summary, byConcept["Por pagar (sin archivo aún)"], "Importe"));

        var banks = report.GetProperty("tables")[1];
        Assert.Equal(["BBVA|SPEI", "Banco Azteca|Traspaso BAZ", "Total|"],
            Rows(banks).Select(r => $"{Text(banks, r, "Banco")}|{Text(banks, r, "Tipo de pago")}").Order(StringComparer.Ordinal));
        var pending = report.GetProperty("tables")[2];
        Assert.Equal(["PÉREZ LÓPEZ CARLA|Sin cuenta", "PÉREZ LÓPEZ DORA|Sin cuenta"],
            Rows(pending).Select(r => $"{Text(pending, r, "Empleado")}|{Text(pending, r, "Motivo")}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Incidencias_and_asistencia_add_up_by_concept_and_by_person()
    {
        var a = await ArrangeAsync();
        foreach (var (who, code, quantity, amount) in new[] { ("ANA", "OVERTIME_DOUBLE", "2", (string?)null), ("BETO", "DEDUCTION", null, "150"), ("ANA", "DEDUCTION", null, "50") })
        {
            var saved = await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
            {
                employeeId = a.Employees[who],
                conceptCode = code,
                date = "2026-09-16",
                quantity,
                amount,
                reason = "Prueba",
            });
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        }
        var incidents = await Report("incidents", $"?periodId={a.Week38}");
        var table = incidents.GetProperty("tables")[0];
        var groups = Rows(table).Where(r => r.GetProperty("emphasis").GetBoolean()).ToDictionary(r => Text(table, r, "Fecha"), r => Value(table, r, "Importe"));
        Assert.Equal(-200m, groups["Descuento"]);
        Assert.Equal(142.85m, groups["Tiempo extra doble"]);
        Assert.Contains(Rows(table), r => !r.GetProperty("emphasis").GetBoolean() && Text(table, r, "Cantidad") == "2 h");

        var attendance = await Report("attendance", $"?clientId={a.Client}&from=2026-09-11&to=2026-09-24");
        var people = attendance.GetProperty("tables")[0];
        var byName = Rows(people).ToDictionary(r => Text(people, r, "Empleado"));
        Assert.Equal("PÉREZ LÓPEZ CARLA", Text(people, Rows(people)[0], "Empleado"));
        Assert.Equal((0m, 2m), (Value(people, byName["PÉREZ LÓPEZ CARLA"], "Faltas")!.Value, Value(people, byName["PÉREZ LÓPEZ CARLA"], "Faltas automáticas")!.Value));
        Assert.Equal(12m, Value(people, byName["PÉREZ LÓPEZ ANA"], "Días laborados"));
        Assert.Equal(2m, Value(people, byName["PÉREZ LÓPEZ BETO"], "Horas extra"));
        var days = attendance.GetProperty("tables")[1];
        Assert.Equal(15, days.GetProperty("columns").GetArrayLength());
        var carla = Rows(days).Single(r => Text(days, r, "Empleado") == "PÉREZ LÓPEZ CARLA");
        Assert.Equal("F*", carla.GetProperty("cells")[2].GetProperty("display").GetString());
    }

    [Fact]
    public async Task Rotacion_and_tarjetas_y_cuentas_follow_altas_bajas_and_accounts()
    {
        var a = await ArrangeAsync();
        Assert.True((await _owner.PostAsJsonAsync($"/v1/employees/{a.Employees["DORA"]}/status",
            new { status = "terminated", startDate = "2026-09-25", endDate = (string?)null, reason = "Renuncia voluntaria", reference = (string?)null })).IsSuccessStatusCode);
        var turnover = await Report("turnover", $"?clientId={a.Client}&from=2026-09-01&to=2026-09-30");
        var months = turnover.GetProperty("tables")[0];
        var september = Rows(months)[0];
        Assert.Equal(("sep 2026", 3m, 1m, 1m, 1m, 3m), (Text(months, september, "Mes"), Value(months, september, "Plantilla al inicio")!.Value,
            Value(months, september, "Altas")!.Value, Value(months, september, "Bajas")!.Value, Value(months, september, "Bajas en 30 días o menos")!.Value,
            Value(months, september, "Plantilla al cierre")!.Value));
        var reasons = turnover.GetProperty("tables")[1];
        Assert.Equal("Renuncia voluntaria", Text(reasons, Rows(reasons)[0], "Motivo"));
        Assert.Equal(2, Rows(turnover.GetProperty("tables")[2]).Count);

        var bbva = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "012").Select(b => b.Uid).SingleAsync());
        await _owner.PostAsJsonAsync($"/v1/employees/{a.Employees["ANA"]}/bank-accounts", new
        {
            bankId = bbva,
            accountType = "clabe",
            accountNumber = "012180000000000015",
            holderName = "María López",
            alias = (string?)null,
            validFrom = (string?)null,
            makePrimary = true,
        });
        await _owner.PostAsJsonAsync($"/v1/employees/{a.Employees["BETO"]}/card-request", new { bank = "banorte" });
        var accounts = await Report("accounts", $"?clientId={a.Client}");
        var summary = accounts.GetProperty("tables")[0];
        Assert.Equal([3, 1, 1, 1, 1], Rows(summary).Select(r => (int)Value(summary, r, "Empleados")!));
        var detail = accounts.GetProperty("tables")[2];
        Assert.Contains(Rows(detail), r => Text(detail, r, "Situación").Contains("A nombre de MARÍA LÓPEZ", StringComparison.Ordinal));
        Assert.Contains(Rows(detail), r => Text(detail, r, "Situación") == "Tarjeta Banorte en espera");
    }

    [Fact]
    public async Task Reportes_de_nomina_need_prenomina_and_follow_the_client_scope()
    {
        var a = await ArrangeAsync();
        var attendanceOnly = await As(UserRole.Administrator, PermissionModule.PayrollAttendance);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendanceOnly.GetAsync("/v1/payroll-reports/cost")).StatusCode);

        var other = await WithDb(db => db.AddClient("HEB"));
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollPrepayroll, ClientId = other.Uid });
        Assert.Empty((await BodyOf(await scoped.GetAsync("/v1/payroll-reports/options"))).GetProperty("periods").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await scoped.GetAsync($"/v1/payroll-reports/cost?clientId={a.Client}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scoped.GetAsync($"/v1/payroll-reports/cost?periodId={a.Week38}")).StatusCode);
        Assert.Empty((await BodyOf(await scoped.GetAsync("/v1/payroll-reports/accounts"))).GetProperty("tables")[2].GetProperty("rows").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await _owner.GetAsync("/v1/payroll-reports/nomina")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.GetAsync("/v1/payroll-reports/cost?from=2026-09-20&to=2026-09-01")).StatusCode);
    }
}
