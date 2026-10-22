using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Catalogs;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>
/// Prenómina through the API (S9): the SEM 38 workbook's 115 rows, as attendance, give the golden totals of PLAN D41; then
/// incidencias, ajustes manuales, retroactivos and the paid-period freeze.
/// </summary>
public sealed class PrepayrollTests(ApiFactory factory) : ApiTest(factory)
{
    private static readonly DateOnly Start = new(2026, 9, 11);
    private static readonly DateOnly End = new(2026, 9, 17);
    private static readonly DateOnly Sunday = new(2026, 9, 13);
    private static readonly DateOnly Holiday = new(2026, 9, 16);

    private static readonly Dictionary<string, (decimal Pocket, decimal Hourly)> Puestos = new()
    {
        ["VALIDADOR"] = (428.57m, 35.71m),
        ["SUPERVISOR"] = (585.71m, 48.8m),
    };

    private HttpClient _owner = null!;

    private sealed record Arranged(Guid Group, Guid Period, Dictionary<string, Guid> Positions, List<Guid> Employees);

    private static decimal Dec(JsonElement e) => e.ValueKind == JsonValueKind.Number
        ? e.GetDecimal()
        : decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    /// <summary>
    /// ECOVIA «Validadores 12 hrs» with the sheet's two puestos and, when <paramref name="rows"/> is null, every SEM 38 row as
    /// an employee with that week's attendance; Semana 38 created through the API.
    /// </summary>
    private async Task<Arranged> ArrangeSem38(int[]? rows = null)
    {
        _owner ??= await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var positions = new Dictionary<string, Guid>();
        foreach (var (name, (pocket, hourly)) in Puestos)
        {
            var body = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", new
            {
                name,
                dailySalaryPocket = pocket.ToString(CultureInfo.InvariantCulture),
                dailySalaryImss = "278.8",
                hourlyRate = hourly.ToString(CultureInfo.InvariantCulture),
                daysWorkedBonus = "0",
                validFrom = "2020-01-01",
            }));
            positions[name] = body.GetProperty("positions").EnumerateArray().Single(p => p.GetProperty("name").GetString() == name)
                .GetProperty("id").GetGuid();
        }
        await WithDb(async db =>
        {
            if (!await db.Set<Holiday>().AnyAsync(h => h.Date == Holiday))
                db.Add(new Holiday { Date = Holiday, NameEs = "Día de la Independencia", IsMandatory = true });
            await db.SaveChangesAsync();
        });

        var sheet = Sem38GoldenTests.Sheet();
        var employees = new List<Guid>();
        await WithDb(async db =>
        {
            foreach (var n in rows ?? Enumerable.Range(6, 115).ToArray())
            {
                var cell = (string letter) => sheet.Cell($"{letter}{n}");
                var words = cell("C").GetString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var person = await db.AddPerson(string.Join(' ', words[2..]), words[0], words[1]);
                var employee = new Employee
                {
                    PersonId = person.Uid,
                    PayrollGroupId = group,
                    PositionId = positions[cell("D").GetString().Trim()],
                    Shift = cell("E").GetString().Trim(),
                    HiredAt = DateOnly.FromDateTime(cell("B").Value.IsDateTime ? cell("B").GetDateTime() : DateTime.FromOADate(cell("B").GetDouble())),
                };
                // A row whose rates are not its puesto's carries them as overrides (row 94: a validador at supervisor rates).
                var puesto = Puestos[cell("D").GetString().Trim()];
                var (pocket, imss, hourly) = ((decimal)Sem38GoldenTests.Number(cell("J")), (decimal)Sem38GoldenTests.Number(cell("I")),
                    (decimal)Sem38GoldenTests.Number(cell("G")));
                if (pocket != puesto.Pocket || hourly != puesto.Hourly || imss != 278.8m)
                    (employee.DailySalaryPocketOverride, employee.DailySalaryImssOverride, employee.HourlyRateOverride) = (pocket, imss, hourly);
                db.Add(employee);
                await db.SaveChangesAsync();
                db.AddRange(Week(sheet, n, employee.Uid, employee.HiredAt));
                employees.Add(employee.Uid);
            }
            await db.SaveChangesAsync();
        });
        var period = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }));
        Assert.Equal((38, "2026-09-11", "2026-09-17"),
            (period.GetProperty("number").GetInt32(), period.GetProperty("startDate").GetString(), period.GetProperty("endDate").GetString()));
        return new Arranged(group, period.GetProperty("id").GetGuid(), positions, employees);
    }

    /// <summary>
    /// A week of attendance that gives back row <paramref name="n"/> under D84: the 16th Festivo when O (a paid día), the
    /// Sunday worked when M (otherwise descanso), one Incompleta of N's hours, P's hours as extra on a worked day, a descanso
    /// laborado when R, and Presente for the rest of F. Days left over stay uncaptured (faltas at the close).
    /// </summary>
    internal static List<AttendanceRecord> Week(IXLWorksheet sheet, int n, Guid employee, DateOnly hired)
    {
        var cell = (string letter) => sheet.Cell($"{letter}{n}");
        var days = (int)Sem38GoldenTests.Number(cell("F"));
        var sunday = !cell("M").IsEmpty();
        var partial = Sem38GoldenTests.HourlyMultiple(cell("N"), n);
        var holiday = !cell("O").IsEmpty();
        var overtime = Sem38GoldenTests.HourlyMultiple(cell("P"), n);
        var rest = !cell("R").IsEmpty();

        var records = new List<AttendanceRecord>();
        AttendanceRecord Add(DateOnly date, AttendanceStatus status, decimal? hours = null)
        {
            var r = new AttendanceRecord { EmployeeId = employee, WorkDate = date, Status = status, HoursWorked = hours, IsLocked = true, RecordedAt = DateTimeOffset.UnixEpoch };
            records.Add(r);
            return r;
        }
        var open = new List<DateOnly>();
        for (var d = Start > hired ? Start : hired; d <= End; d = d.AddDays(1))
            open.Add(d);
        if (holiday)
        {
            Add(Holiday, AttendanceStatus.Holiday);
            open.Remove(Holiday);
            days--;
        }
        var worked = new List<(AttendanceStatus Status, decimal? Hours)>();
        if (rest)
            worked.Add((AttendanceStatus.RestDayWorked, 12));
        while (worked.Count < days)
            worked.Add((AttendanceStatus.Present, 12));
        if (partial > 0)
            worked.Add((AttendanceStatus.Partial, partial));
        // The Sunday takes the first worked day when M, and is a descanso otherwise.
        var slots = open.Where(d => d != Sunday).ToList();
        if (open.Contains(Sunday))
        {
            if (sunday)
                slots.Insert(0, Sunday);
            else
                Add(Sunday, AttendanceStatus.RestDay);
        }
        Assert.True(worked.Count <= slots.Count, $"row {n}: {worked.Count} worked days do not fit in {slots.Count}");
        for (var i = 0; i < worked.Count; i++)
            Add(slots[i], worked[i].Status, worked[i].Hours);
        if (overtime > 0)
            records.First(r => r.Status is AttendanceStatus.Present or AttendanceStatus.RestDayWorked).OvertimeHours = overtime;
        return records;
    }

    private async Task<JsonElement> Grid(Guid period) => await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{period}"));

    private static JsonElement Row(JsonElement grid, Guid employee) =>
        grid.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == employee);

    private static decimal? Cell(JsonElement grid, JsonElement row, string column)
    {
        var i = grid.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ToList().IndexOf(column);
        var v = row.GetProperty("amounts")[i];
        return v.ValueKind == JsonValueKind.Null ? null : Dec(v);
    }

    [Fact]
    public async Task Semana_38_as_attendance_gives_the_golden_totals_of_D41()
    {
        var a = await ArrangeSem38();
        var close = await _owner.PostAsync($"/v1/payroll/periods/{a.Period}/close-capture", null);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var grid = await BodyOf(close);

        Assert.Equal("in_review", grid.GetProperty("period").GetProperty("status").GetString());

        // Row by row, the cells are the sheet's (O differs by D41's known amounts, so it is left out here).
        var sheet = Sem38GoldenTests.Sheet();
        var mismatches = new List<string>();
        foreach (var (letter, column) in new[] { ("F", ""), ("J", "J"), ("G", "G"), ("K", "SUELDO"), ("L", "SEPTIMO DIA"), ("M", "PRIMA DOMINICAL"),
                     ("N", "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS"), ("P", "HORAS EXTRA"), ("R", "DESCANSO LABORADO") })
            for (var i = 0; i < a.Employees.Count; i++)
            {
                var row = Row(grid, a.Employees[i]);
                var sheetValue = (decimal)Sem38GoldenTests.Number(sheet.Cell($"{letter}{i + 6}"));
                var actual = column switch
                {
                    "" => Dec(row.GetProperty("daysWorked")),
                    "J" => Dec(row.GetProperty("dailySalaryPocket")),
                    "G" => Dec(row.GetProperty("hourlyRate")),
                    _ => Cell(grid, row, column) ?? 0,
                };
                if (actual != (column is "" or "J" or "G" ? sheetValue : decimal.Truncate(sheetValue * 100m) / 100m))
                    mismatches.Add($"{letter}{i + 6}: sheet {sheetValue}, grid {actual}");
            }
        Assert.True(mismatches.Count == 0, string.Join('\n', mismatches));
        Assert.Equal(115, grid.GetProperty("rows").GetArrayLength());
        Assert.Equal(334_654.83m, Dec(grid.GetProperty("totalNet")));
        Assert.Equal(334_655.149146m, Dec(grid.GetProperty("totalNetExact")));
        Assert.Equal(334_654.83m, Dec(grid.GetProperty("period").GetProperty("totalNet")));

        // The client's columns K–U in their order; each footer is Σ of the cells shown (D76).
        Assert.Equal(["SUELDO", "SEPTIMO DIA", "PRIMA DOMINICAL", "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS", "FESTIVO PRIMERA MITAD",
                "HORAS EXTRA", "RETROACTIVO", "DESCANSO LABORADO", "TIEMPO EXTRA DOBLE", "FESTIVO LABORADO", "DESCUENTO"],
            grid.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        var rows = grid.GetProperty("rows").EnumerateArray().ToList();
        var footers = grid.GetProperty("footers").EnumerateArray().Select(Dec).ToList();
        for (var c = 0; c < footers.Count; c++)
            Assert.Equal(rows.Sum(r => r.GetProperty("amounts")[c] is { ValueKind: JsonValueKind.Number or JsonValueKind.String } v ? Dec(v) : 0), footers[c]);
        Assert.Equal(1_500m, footers[7]);

        // No uncaptured day went unrecorded: every day after the hire date has a record now.
        Assert.Equal(0, await WithDb(db => db.Set<AttendanceRecord>().CountAsync(r => r.IsAutoAbsent && r.Status != AttendanceStatus.Absent)));
        Assert.Equal(HttpStatusCode.Conflict, (await _owner.PostAsync($"/v1/payroll/periods/{a.Period}/close-capture", null)).StatusCode);
    }

    private Task<PrepayrollLine> LineOf(Guid employee) =>
        WithDb(db => db.Set<PrepayrollLine>().AsNoTracking().SingleAsync(l => l.EmployeeId == employee));

    [Fact]
    public async Task A_past_dated_baja_stops_paying_the_days_after_it_without_deleting_them()
    {
        var a = await ArrangeSem38([6]);
        var employee = a.Employees[0];
        var before = await LineOf(employee);
        var baja = new DateOnly(2026, 9, 14);

        var response = await _owner.PostAsJsonAsync($"/v1/employees/{employee}/status",
            new { status = "terminated", startDate = "2026-09-14", endDate = (string?)null, reason = "Renuncia voluntaria", reference = (string?)null, confirmPaidDays = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var records = await WithDb(db => db.Set<AttendanceRecord>().Where(r => r.EmployeeId == employee).ToListAsync());
        Assert.Contains(records, r => r.WorkDate >= baja && r.Status == AttendanceStatus.Present);
        var paid = new[] { AttendanceStatus.Present, AttendanceStatus.RestDayWorked, AttendanceStatus.HolidayWorked };
        var after = await LineOf(employee);
        Assert.Equal(records.Count(r => r.WorkDate < baja && paid.Contains(r.Status)), after.DaysWorked);
        Assert.True(after.DaysWorked < before.DaysWorked);
        Assert.True(after.TotalNet < before.TotalNet);

        // The same with the alta moved to a later date than some of the attendance.
        var hired = new DateOnly(2026, 9, 15);
        await WithDb(async db =>
        {
            var e = await db.Set<Employee>().SingleAsync(x => x.Uid == employee);
            (e.TerminatedAt, e.HiredAt) = (null, hired);
            await db.SaveChangesAsync();
        });
        await WithDb(db => Prepayrolls.SaveAndRefreshEmployeeAsync(db, employee, DateTimeOffset.UtcNow, CancellationToken.None));
        var late = await LineOf(employee);
        Assert.Equal(records.Count(r => r.WorkDate >= hired && paid.Contains(r.Status)), late.DaysWorked);
    }

    [Fact]
    public async Task Changing_the_holiday_catalog_recomputes_the_open_lines_that_cover_the_date()
    {
        var a = await ArrangeSem38([6]);
        var employee = a.Employees[0];
        var date = new DateOnly(2026, 9, 15);
        await WithDb(async db =>
        {
            db.RemoveRange(db.Set<AttendanceRecord>().Where(r => r.EmployeeId == employee && r.WorkDate == date));
            db.Add(new AttendanceRecord { EmployeeId = employee, WorkDate = date, Status = AttendanceStatus.Holiday, IsLocked = true, RecordedAt = DateTimeOffset.UnixEpoch });
            await db.SaveChangesAsync();
        });
        Assert.Equal(0, (await LineOf(employee)).HolidaysEntitled);

        var created = await _owner.PostAsJsonAsync("/v1/catalogs/holidays", new { date = "2026-09-15", nameEs = "Feriado de prueba", isMandatory = false });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var line = await LineOf(employee);
        Assert.Equal(1, line.HolidaysEntitled);
        var withHoliday = line.DaysWorked;

        var holiday = (await BodyOf(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await _owner.PutAsJsonAsync($"/v1/catalogs/holidays/{holiday}",
            new { date = "2026-09-20", nameEs = "Feriado de prueba", isMandatory = false })).StatusCode);
        Assert.Equal(0, (await LineOf(employee)).HolidaysEntitled);

        Assert.Equal(HttpStatusCode.OK, (await _owner.PutAsJsonAsync($"/v1/catalogs/holidays/{holiday}",
            new { date = "2026-09-15", nameEs = "Feriado de prueba", isMandatory = false })).StatusCode);
        Assert.Equal(withHoliday, (await LineOf(employee)).DaysWorked);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.DeleteAsync($"/v1/catalogs/holidays/{holiday}")).StatusCode);
        Assert.Equal(0, (await LineOf(employee)).HolidaysEntitled);
        Assert.True((await LineOf(employee)).DaysWorked < withHoliday);
    }

    [Fact]
    public async Task An_incidencia_recomputes_the_line_and_its_desglose_shows_where_each_amount_comes_from()
    {
        var a = await ArrangeSem38([6]);
        var employee = a.Employees[0];
        var before = Row(await Grid(a.Period), employee);
        Assert.Equal(2_999.99m, Dec(before.GetProperty("totalNet")));

        var doubles = await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
        {
            employeeId = employee,
            conceptCode = "OVERTIME_DOUBLE",
            date = "2026-09-12",
            quantity = "2",
            amount = (string?)null,
            reason = "Evento",
        });
        Assert.Equal(HttpStatusCode.OK, doubles.StatusCode);
        Assert.False((await BodyOf(doubles)).GetProperty("duringReview").GetBoolean());
        var deduction = await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
        {
            employeeId = employee,
            conceptCode = "DEDUCTION",
            date = "2026-09-15",
            quantity = (string?)null,
            amount = "100",
            reason = "Uniforme",
        }));

        var grid = await Grid(a.Period);
        var row = Row(grid, employee);
        Assert.Equal(142.84m, Cell(grid, row, "TIEMPO EXTRA DOBLE"));
        Assert.Equal(-100m, Cell(grid, row, "DESCUENTO"));
        Assert.Equal(2_999.99m + 142.84m - 100m, Dec(row.GetProperty("totalNet")));

        var line = await BodyOf(await _owner.GetAsync($"/v1/payroll/lines/{row.GetProperty("lineId").GetGuid()}"));
        var items = line.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["SALARY", "SEVENTH_DAY", "OVERTIME_DOUBLE", "DEDUCTION"], items.Select(i => i.GetProperty("code").GetString()));
        Assert.Equal("Uniforme", items[3].GetProperty("reason").GetString());
        Assert.Equal(7, line.GetProperty("days").GetArrayLength());
        Assert.Equal(2, line.GetProperty("incidents").GetArrayLength());

        // The Incidencias screen's pickers.
        Assert.Contains((await BodyOf(await _owner.GetAsync("/v1/payroll/incidents/periods"))).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == a.Period);
        var picks = await BodyOf(await _owner.GetAsync($"/v1/payroll/incidents/employees?periodId={a.Period}"));
        Assert.Equal(employee, Assert.Single(picks.EnumerateArray()).GetProperty("employeeId").GetGuid());

        // Not attendance concepts, and hours or an amount as the concept needs.
        var bad = await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
        {
            employeeId = employee,
            conceptCode = "REST_DAY_WORKED",
            date = "2026-09-12",
            quantity = (string?)null,
            amount = "500",
            reason = (string?)null,
        });
        Assert.Contains("conceptCode", (await ErrorsOf(bad)).Keys);
        var noHours = await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
        {
            employeeId = employee,
            conceptCode = "OVERTIME_TRIPLE",
            date = "2026-09-12",
            quantity = (string?)null,
            amount = "10",
            reason = (string?)null,
        });
        Assert.Contains("quantity", (await ErrorsOf(noHours)).Keys);

        Assert.Equal(HttpStatusCode.NoContent, (await _owner.DeleteAsync($"/v1/payroll/incidents/{deduction.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(2_999.99m + 142.84m, Dec(Row(await Grid(a.Period), employee).GetProperty("totalNet")));
    }

    [Fact]
    public async Task Editing_a_cell_is_an_ajuste_manual_of_the_difference_with_its_reason()
    {
        var a = await ArrangeSem38([6]);
        var employee = a.Employees[0];
        var lineId = Row(await Grid(a.Period), employee).GetProperty("lineId").GetGuid();

        var noReason = await _owner.PostAsJsonAsync($"/v1/payroll/lines/{lineId}/adjustments", new { column = "SUELDO", amount = "2600", reason = " " });
        Assert.Contains("reason", (await ErrorsOf(noReason)).Keys);
        var adjusted = await _owner.PostAsJsonAsync($"/v1/payroll/lines/{lineId}/adjustments", new { column = "SUELDO", amount = "2600", reason = "Pago de turno doble" });
        Assert.Equal(HttpStatusCode.OK, adjusted.StatusCode);
        var manual = (await BodyOf(adjusted)).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("source").GetString() == "manual");
        Assert.Equal((28.58m, "SUELDO", "Pago de turno doble"),
            (Dec(manual.GetProperty("amount")), manual.GetProperty("column").GetString(), manual.GetProperty("reason").GetString()));

        var grid = await Grid(a.Period);
        var row = Row(grid, employee);
        Assert.Equal(2_600m, Cell(grid, row, "SUELDO"));
        Assert.True(row.GetProperty("adjusted").GetBoolean());
        Assert.Equal(2_999.99m + 28.58m, Dec(row.GetProperty("totalNet")));

        // A DESCUENTO is entered as shown, negative.
        await _owner.PostAsJsonAsync($"/v1/payroll/lines/{lineId}/adjustments", new { column = "DESCUENTO", amount = "-50", reason = "Faltante de caja" });
        grid = await Grid(a.Period);
        Assert.Equal(-50m, Cell(grid, Row(grid, employee), "DESCUENTO"));
        Assert.Equal(2_999.99m + 28.58m - 50m, Dec(Row(grid, employee).GetProperty("totalNet")));

        // The comments never carry bank data.
        var bank = await _owner.PutAsJsonAsync($"/v1/payroll/lines/{lineId}/comments", new { comments = "BBVA / 0125 8001 5505 682921" });
        Assert.Equal([PrepayrollEndpoints.NoBankData], (await ErrorsOf(bank))["comments"]);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.PutAsJsonAsync($"/v1/payroll/lines/{lineId}/comments", new { comments = "AGREGAR PRIMA" })).StatusCode);
        Assert.Equal("AGREGAR PRIMA", Row(await Grid(a.Period), employee).GetProperty("comments").GetString());
    }

    [Fact]
    public async Task A_paid_period_is_frozen_and_a_late_incidencia_goes_to_the_next_open_one_as_a_retroactivo()
    {
        var a = await ArrangeSem38([6]);
        var employee = a.Employees[0];
        var lineId = Row(await Grid(a.Period), employee).GetProperty("lineId").GetGuid();
        await WithDb(db => db.Set<PayrollPeriod>().Where(p => p.Uid == a.Period)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PeriodStatus.Paid)));

        var retro = await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/incidents", new
        {
            employeeId = employee,
            conceptCode = "RETROACTIVE",
            date = "2026-09-12",
            quantity = (string?)null,
            amount = "250",
            reason = "Día no pagado",
        }));
        Assert.Equal((39, 38), (retro.GetProperty("periodNumber").GetInt32(), retro.GetProperty("originPeriodNumber").GetInt32()));
        var next = retro.GetProperty("periodId").GetGuid();
        var grid = await Grid(next);
        Assert.Equal(250m, Cell(grid, Row(grid, employee), "RETROACTIVO"));

        // The paid line does not change, through the API or the database.
        Assert.Equal(2_999.99m, Dec(Row(await Grid(a.Period), employee).GetProperty("totalNet")));
        Assert.False((await Grid(a.Period)).GetProperty("editable").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,
            (await _owner.PostAsJsonAsync($"/v1/payroll/lines/{lineId}/adjustments", new { column = "SUELDO", amount = "1", reason = "x" })).StatusCode);
        var raised = await Assert.ThrowsAnyAsync<Exception>(() => WithDb(db =>
            db.Set<PrepayrollLine>().Where(l => l.Uid == lineId).ExecuteUpdateAsync(s => s.SetProperty(l => l.Comments, "cambio"))));
        Assert.Contains("paid", raised.Message + raised.InnerException?.Message, StringComparison.OrdinalIgnoreCase);

        // New rates reach open periods only: 39 is recomputed, 38 keeps what it paid.
        await _owner.PostAsJsonAsync($"/v1/payroll/positions/{a.Positions["VALIDADOR"]}/rates", new
        {
            dailySalaryPocket = "450",
            dailySalaryImss = "278.8",
            hourlyRate = "37.5",
            daysWorkedBonus = "0",
            validFrom = "2026-09-01",
        });
        Assert.Equal(2_999.99m, Dec(Row(await Grid(a.Period), employee).GetProperty("totalNet")));
        Assert.Equal(450m, Dec(Row(await Grid(next), employee).GetProperty("dailySalaryPocket")));
    }

    [Fact]
    public async Task The_period_excel_has_the_client_layout_and_the_pdfs_render()
    {
        var a = await ArrangeSem38([6, 94, 118]);
        await _owner.PostAsync($"/v1/payroll/periods/{a.Period}/close-capture", null);

        var excel = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/periods/{a.Period}/exports", new { kind = "excel" }));
        Assert.Equal("PRENOMINA SEM 38 ECOVIA VALIDADORES 12 HRS.xlsx", excel.GetProperty("fileName").GetString());
        var (_, key) = Factory.Storage.Downloads.Single(d => d.DownloadAs == excel.GetProperty("fileName").GetString()) is var d ? (d.Bucket, d.Key) : default;
        using var book = new XLWorkbook(new MemoryStream(Factory.Storage.Contents[(Platform.Core.Abstractions.StorageBucket.Exports, key)]));
        var sheet = book.Worksheet("SEM 38");
        var client = Sem38GoldenTests.Sheet();
        foreach (var letter in "BCDEFGHIJKLMNOPQRSTUVW")
            Assert.Equal(client.Cell($"{letter}5").GetString().Trim(), sheet.Cell($"{letter}5").GetString());
        Assert.Equal(("SEMANA N° 38", "11 AL 17 DE SEPTIEMBRE 2026"), (sheet.Cell("C4").GetString(), sheet.Cell("F4").GetString()));
        Assert.Equal("CARREON MONSIVAIS ROBERTO EDUARDO", sheet.Cell("C6").GetString());
        Assert.Equal(new DateTime(2023, 9, 15), sheet.Cell("B6").GetDateTime());
        Assert.Equal(2_999.99, sheet.Cell("V6").GetDouble());
        // Footers: Σ of what is shown, the payable total under V.
        var grid = await Grid(a.Period);
        Assert.Equal((double)Dec(grid.GetProperty("totalNet")), sheet.Cell("V9").GetDouble());
        Assert.Equal(sheet.Cell("K6").GetDouble() + sheet.Cell("K7").GetDouble() + sheet.Cell("K8").GetDouble(), sheet.Cell("K9").GetDouble(), 6);

        foreach (var kind in new[] { "summary", "receipts" })
        {
            var pdf = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/periods/{a.Period}/exports", new { kind }));
            var file = Factory.Storage.Downloads.Single(x => x.DownloadAs == pdf.GetProperty("fileName").GetString());
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(Factory.Storage.Contents[(file.Bucket, file.Key)][..4]));
        }
        Assert.Equal(3, await WithDb(db => db.Set<Platform.Core.Audit.AuditEntry>().CountAsync(e => e.Action == Platform.Core.Audit.AuditAction.Export)));
    }

    [Theory]
    [InlineData("NOE ALANIS TREVINO", "ALANÍS TREVIÑO NOÉ", true)]
    [InlineData("carreon monsivais roberto eduardo", "CARREON MONSIVAIS ROBERTO EDUARDO", true)]
    [InlineData("LETICIA MORA SANCHEZ", "ALANÍS TREVIÑO NOÉ", false)]
    public void The_holder_is_the_employee_whatever_the_order_case_or_accents(string holder, string fullName, bool same) =>
        Assert.Equal(same, PrepayrollQueries.SameName(holder, fullName));

    [Fact]
    public async Task Prenomina_needs_its_grant_and_follows_the_client_scope()
    {
        var a = await ArrangeSem38([6]);
        var attendanceOnly = await As(UserRole.Administrator, PermissionModule.PayrollAttendance);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendanceOnly.GetAsync($"/v1/payroll/periods/{a.Period}")).StatusCode);

        var other = await WithDb(db => db.AddClient("HEB"));
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollPrepayroll, ClientId = other.Uid });
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.GetAsync($"/v1/payroll/periods/{a.Period}")).StatusCode);
        Assert.Empty((await BodyOf(await scoped.GetAsync("/v1/payroll/periods"))).EnumerateArray());
    }
}
