using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Platform.Bank.Payments;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Reports;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;
using BankRow = Platform.Core.Catalogs.Bank;

namespace Platform.Bank.Reports;

/// <summary>
/// Reportes de nómina (SPEC §5.9, PLAN D87): each report read from the same queries the screens use — report 1 from the
/// prenómina grid of each period, so its columns are the grid's footers and its total is what is paid (D76); report 2 from the
/// payment files; 4 counts días with the prenómina's own rule (D84).
/// </summary>
public static class PayrollReports
{
    public const string UnknownClient = "El cliente no existe o no es de tus clientes.";
    public const string UnknownPeriod = "El período no existe o no es de tus clientes.";
    public const string RangeReversed = "La fecha final es anterior a la inicial.";
    public const string RangeTooLong = "El rango admite hasta un año.";

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-MX");
    private static readonly string[] Months = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public static string Title(PayrollReport report) => report switch
    {
        PayrollReport.Cost => "Costo de nómina por cliente",
        PayrollReport.Payments => "Resumen de pago",
        PayrollReport.Incidents => "Incidencias del período",
        PayrollReport.Attendance => "Asistencia por empleado",
        PayrollReport.Turnover => "Rotación",
        _ => "Tarjetas y cuentas",
    };

    /// <summary>A period as the pickers and filter lines name it.</summary>
    private sealed record PeriodInfo(PayrollPeriod Period, PayrollGroup Group, string Client)
    {
        public string Label => $"{Client} · {Group.Name} · {(Group.PeriodType == PeriodType.Weekly ? "Semana" : "Quincena")} {Period.PeriodNumber} · {Short(Period.StartDate)} – {Short(Period.EndDate)}";
    }

    private static string Short(DateOnly d) => $"{d.Day} {Months[d.Month - 1]}";

    private static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Month(DateOnly d) => $"{Months[d.Month - 1]} {d.Year}";

    private static async Task<List<PeriodInfo>> PeriodsAsync(DbContext db, IQueryable<PayrollGroup> groups, Guid? clientId, CancellationToken ct)
    {
        var rows = await db.Set<PayrollPeriod>().AsNoTracking()
            .Where(p => db.Set<PrepayrollLine>().Any(l => l.PayrollPeriodId == p.Uid))
            .Join(groups.Where(g => clientId == null || g.ClientId == clientId), p => p.PayrollGroupId, g => g.Uid, (p, g) => new { p, g })
            .Join(db.Set<Client>(), x => x.g.ClientId, c => c.Uid, (x, c) => new { x.p, x.g, c.ShortName })
            .OrderByDescending(x => x.p.StartDate).ThenBy(x => x.ShortName).ThenBy(x => x.g.Name).ToListAsync(ct);
        return rows.Select(x => new PeriodInfo(x.p, x.g, x.ShortName)).ToList();
    }

    public static async Task<PayrollReportOptionsDto> OptionsAsync(DbContext db, IQueryable<PayrollGroup> groups, CancellationToken ct)
    {
        var clients = await db.Set<Client>().Where(c => groups.Any(g => g.ClientId == c.Uid)).OrderBy(c => c.ShortName)
            .Select(c => new PayrollReportClientDto(c.Uid, c.ShortName)).ToListAsync(ct);
        var periods = (await PeriodsAsync(db, groups, null, ct))
            .Select(p => new PayrollReportPeriodDto(p.Period.Uid, p.Group.ClientId, p.Label, p.Period.Status, p.Period.StartDate, p.Period.EndDate))
            .ToList();
        return new PayrollReportOptionsDto(clients, periods);
    }

    /// <summary>The filters resolved against the grant (and the defaults): a period for Semana reports, a range otherwise.</summary>
    private sealed record Resolved(PayrollReportFiltersDto Filters, PeriodInfo? Period, List<PeriodInfo> Periods);

    public static async Task<(PayrollReportDto? Report, string? Field, string? Problem)> BuildAsync(DbContext db, IQueryable<PayrollGroup> groups,
        PayrollReport report, Guid? clientId, Guid? periodId, DateOnly? from, DateOnly? to, DateOnly today, CancellationToken ct)
    {
        string? clientName = null;
        if (clientId is { } c)
        {
            clientName = await db.Set<Client>().Where(x => x.Uid == c && groups.Any(g => g.ClientId == c)).Select(x => x.ShortName)
                .SingleOrDefaultAsync(ct);
            if (clientName is null)
                return (null, "clientId", UnknownClient);
        }
        if (from is { } f0 && to is { } t0 && t0 < f0)
            return (null, "to", RangeReversed);
        if (from is { } f1 && to is { } t1 && t1.DayNumber - f1.DayNumber > 370)
            return (null, "to", RangeTooLong);

        var periods = await PeriodsAsync(db, groups, clientId, ct);
        PeriodInfo? period = null;
        if (periodId is { } pid)
        {
            period = (await PeriodsAsync(db, groups, null, ct)).SingleOrDefault(p => p.Period.Uid == pid);
            if (period is null || (clientId is { } cc && period.Group.ClientId != cc))
                return (null, "periodId", UnknownPeriod);
        }
        var weekly = report is PayrollReport.Cost or PayrollReport.Payments or PayrollReport.Incidents or PayrollReport.Attendance;
        // Semana by default: the last paid period, else the last in review, else the latest.
        if (weekly && period is null && (report == PayrollReport.Payments || from is null || to is null))
            period = periods.FirstOrDefault(p => p.Period.Status == PeriodStatus.Paid)
                ?? periods.FirstOrDefault(p => p.Period.Status == PeriodStatus.InReview) ?? periods.FirstOrDefault();

        PayrollReportFiltersDto filters;
        var who = clientName ?? "Todos los clientes";
        if (report == PayrollReport.Accounts)
            filters = new(clientId, clientName, null, null, null, null, $"{who} · al {Date(today)}");
        else if (report == PayrollReport.Turnover)
        {
            var start = from ?? new DateOnly(today.Year, 1, 1);
            var end = to ?? today;
            start = new DateOnly(start.Year, start.Month, 1);
            end = new DateOnly(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month));
            filters = new(clientId, clientName, null, null, start, end, $"{who} · {Month(start)} – {Month(end)}");
        }
        else if (period is not null)
            filters = new(clientId ?? (periodId is null ? null : period.Group.ClientId), clientName ?? (periodId is null ? null : period.Client),
                period.Period.Uid, period.Label, period.Period.StartDate, period.Period.EndDate, period.Label);
        else if (from is { } f && to is { } t)
            filters = new(clientId, clientName, null, null, f, t, $"{who} · {Date(f)} – {Date(t)}");
        else
            filters = new(clientId, clientName, null, null, null, null, $"{who} · sin períodos");

        var r = new Resolved(filters, period, periods);
        var dto = report switch
        {
            PayrollReport.Cost => await CostAsync(db, groups, r, today, ct),
            PayrollReport.Payments => await PaymentsAsync(db, r, today, ct),
            PayrollReport.Incidents => await IncidentsAsync(db, r, ct),
            PayrollReport.Attendance => await AttendanceAsync(db, groups, r, ct),
            PayrollReport.Turnover => await TurnoverAsync(db, groups, r, ct),
            _ => await AccountsAsync(db, groups, r, today, ct),
        };
        return (dto, null, null);
    }

    /// <summary>The periods a Semana or a range covers: the one chosen, or those ending inside the range.</summary>
    private static List<PeriodInfo> Covered(Resolved r) => r.Period is { } p
        ? [p]
        : r.Filters.From is { } f && r.Filters.To is { } t
            ? r.Periods.Where(x => x.Period.EndDate >= f && x.Period.EndDate <= t).OrderBy(x => x.Client).ThenBy(x => x.Group.Name)
                .ThenBy(x => x.Period.StartDate).ToList()
            : [];

    // ---- 1 · Costo de nómina por cliente ----

    private static readonly (string Header, string[] Columns)[] CostColumns =
    [
        ("Sueldo", ["SUELDO"]),
        ("Séptimo día", ["SEPTIMO DIA"]),
        ("Prima dominical", ["PRIMA DOMINICAL"]),
        ("Horas incompletas", ["MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS"]),
        ("Horas extra", ["HORAS EXTRA"]),
        ("Extra doble", ["TIEMPO EXTRA DOBLE"]),
        ("Extra triple", ["TIEMPO EXTRA TRIPLE"]),
        ("Festivos", ["FESTIVO PRIMERA MITAD", "FESTIVO LABORADO"]),
        ("Descanso laborado", ["DESCANSO LABORADO"]),
        ("Bonos", ["BONO DIAS TRABAJADOS", "BONO PUNTUALIDAD Y ASISTENCIA", "BONO PRODUCTIVIDAD"]),
        ("Retroactivos", ["RETROACTIVO"]),
        ("Descuentos", ["DESCUENTO"]),
    ];

    private static async Task<PayrollReportDto> CostAsync(DbContext db, IQueryable<PayrollGroup> groups, Resolved r, DateOnly today, CancellationToken ct)
    {
        var covered = Covered(r);
        var rows = new List<PayrollReportRowDto>();
        var sums = new decimal[CostColumns.Length];
        var (people, total) = (0, 0m);
        foreach (var byGroup in covered.GroupBy(p => p.Group.Uid))
        {
            // Δ against the group's period just before, whether or not it is in the range.
            var first = byGroup.First();
            var before = r.Periods.Where(p => p.Group.Uid == byGroup.Key && p.Period.StartDate < first.Period.StartDate)
                .MaxBy(p => p.Period.StartDate);
            decimal? previous = before is null ? null : (await PrepayrollQueries.Grid(db, groups, before.Period.Uid, today, ct))!.TotalNet;
            foreach (var p in byGroup)
            {
                var grid = (await PrepayrollQueries.Grid(db, groups, p.Period.Uid, today, ct))!;
                var amounts = CostColumns.Select(c => c.Columns.Sum(col => grid.Columns.ToList().IndexOf(col) is var i and >= 0 ? grid.Footers[i] : 0m))
                    .ToList();
                var paid = grid.Rows.Count(x => x.TotalNet > 0);
                var delta = previous is { } prev ? grid.TotalNet - prev : (decimal?)null;
                rows.Add(new PayrollReportRowDto(
                [
                    ReportCell.Text($"{(p.Group.PeriodType == PeriodType.Weekly ? "Semana" : "Quincena")} {p.Period.PeriodNumber} · {Short(p.Period.StartDate)} – {Short(p.Period.EndDate)}"),
                    ReportCell.Text($"{p.Client} · {p.Group.Name}"),
                    ReportCell.Text(Status(p.Period.Status)),
                    ReportCell.Count(paid),
                    .. amounts.Select(a => ReportCell.Money(a)),
                    ReportCell.Money(grid.TotalNet),
                    ReportCell.Money(delta),
                    ReportCell.Percent(delta is { } d && previous is { } pv && pv != 0 ? d / pv : null),
                ], PeriodId: p.Period.Uid));
                for (var i = 0; i < sums.Length; i++)
                    sums[i] += amounts[i];
                people += paid;
                total += grid.TotalNet;
                previous = grid.TotalNet;
            }
        }
        if (rows.Count > 1)
            rows.Add(new PayrollReportRowDto(
            [
                ReportCell.Text("Total"), ReportCell.Text($"{rows.Count} períodos"), ReportCell.Blank, ReportCell.Count(people),
                .. sums.Select(s => ReportCell.Money(s)), ReportCell.Money(total), ReportCell.Blank, ReportCell.Blank,
            ], Emphasis: true));
        var columns = new List<string> { "Período", "Cliente · grupo", "Estado", "Empleados pagados" };
        columns.AddRange(CostColumns.Select(c => c.Header));
        columns.AddRange(["Total neto", "Δ $", "Δ %"]);
        return new PayrollReportDto(PayrollReport.Cost, Title(PayrollReport.Cost), r.Filters, [new(null, columns, rows)],
            "Cada columna suma los importes que muestra la prenómina (informativos); el total neto es lo que se paga, la suma de cada persona. "
            + "Δ compara con el período anterior del mismo grupo.");
    }

    private static string Status(PeriodStatus s) => s switch
    {
        PeriodStatus.Capturing => "En captura",
        PeriodStatus.InReview => "En revisión",
        _ => "Pagado",
    };

    // ---- 2 · Resumen de pago ----

    private static async Task<PayrollReportDto> PaymentsAsync(DbContext db, Resolved r, DateOnly today, CancellationToken ct)
    {
        var title = Title(PayrollReport.Payments);
        if (r.Period is not { } p)
            return new(PayrollReport.Payments, title, r.Filters, [], "No hay períodos con prenómina.");
        var lines = await PaymentFiles.LoadAsync(db, p.Period.Uid, today, ct);
        var batches = await db.Set<PaymentBatch>().AsNoTracking().Include(b => b.Rows).Where(b => b.PayrollPeriodId == p.Period.Uid).ToListAsync(ct);
        var rows = batches.SelectMany(b => b.Rows.Select(x => (Batch: b, Row: x))).ToList();
        var states = lines.Select(x => (Line: x, State: PaymentFiles.State(x, p.Period.Status))).ToList();
        var totalNet = lines.Sum(x => x.Entity.TotalNet);
        PayrollReportRowDto Line(string label, IEnumerable<decimal> amounts, bool emphasis = false)
        {
            var list = amounts.ToList();
            return new([ReportCell.Text(label), ReportCell.Count(list.Count), ReportCell.Money(list.Sum())], emphasis);
        }
        // Owed and not paid by any file: before payment, everyone owed; after, the lines rejected, sin cuenta or inválidas.
        var unpaid = states.Where(x => x.Line.Entity.TotalNet > 0 && x.Line.Entity.PaymentStatus is not (PaymentRowStatus.Pending or PaymentRowStatus.Paid))
            .ToList();
        // The summary is by line, so its rows add up to the period's total (PLAN D97: a row the bank rejected and the file that paid
        // it again are one person, counted once, where they stand now).
        var owed = lines.Where(x => x.Entity.TotalNet > 0).ToList();
        decimal[] Net(IEnumerable<PaymentFiles.Line> xs) => xs.Select(x => x.Entity.TotalNet).ToArray();
        var resent = lines.Where(x => x.Entity.PaymentStatus != PaymentRowStatus.Rejected
            && rows.Any(r => r.Row.PrepayrollLineId == x.Entity.Uid && r.Row.Status == PaymentRowStatus.Rejected)).ToList();
        var summary = new List<PayrollReportRowDto>
        {
            new([ReportCell.Text("Total neto del período"), ReportCell.Count(owed.Count), ReportCell.Money(totalNet)], true),
            Line("Pagado por el banco", Net(owed.Where(x => x.Entity.PaymentStatus == PaymentRowStatus.Paid && x.Manual is null))),
            Line("Pagado fuera de archivo", Net(owed.Where(x => x.Manual is not null))),
            Line("En archivo sin subir", Net(owed.Where(x => x.Entity.PaymentStatus == PaymentRowStatus.Pending))),
            Line("Rechazado por el banco (pendiente de reenviar)", Net(owed.Where(x => x.Entity.PaymentStatus == PaymentRowStatus.Rejected))),
            Line("Por pagar (sin archivo aún)", Net(owed.Where(x => x.Entity.PaymentStatus is null or PaymentRowStatus.MissingAccount or PaymentRowStatus.InvalidAccount))),
            Line("Reenviados tras un rechazo (ya incluidos arriba)", Net(resent)),
        };
        var banks = await db.Set<BankRow>().AsNoTracking().GroupBy(b => b.SpeiCode).Select(g => new { g.Key, Name = g.Min(b => b.Name) })
            .ToDictionaryAsync(x => x.Key, x => x.Name!, ct);
        var byBank = rows.Where(x => x.Row.Status != PaymentRowStatus.Rejected)
            .GroupBy(x => (Bank: banks.GetValueOrDefault(x.Row.ReceivingBankCode, x.Row.ReceivingBankCode), x.Row.PaymentType))
            .OrderBy(g => g.Key.Bank).ThenBy(g => g.Key.PaymentType)
            .Select(g => new PayrollReportRowDto([ReportCell.Text(g.Key.Bank),
                ReportCell.Text(g.Key.PaymentType == PaymentType.BazTransfer ? "Traspaso BAZ" : "SPEI"), ReportCell.Count(g.Count()),
                ReportCell.Money(g.Sum(x => x.Row.Amount))]))
            .ToList();
        if (byBank.Count > 1)
            byBank.Add(new([ReportCell.Text("Total"), ReportCell.Blank, ReportCell.Count(byBank.Sum(x => (int)x.Cells[2].Value!)),
                ReportCell.Money(byBank.Sum(x => x.Cells[3].Value!.Value))], true));
        var reasons = rows.Where(x => x.Row.Status == PaymentRowStatus.Rejected).GroupBy(x => x.Row.PrepayrollLineId)
            .ToDictionary(g => g.Key, g => g.Last().Row.RejectionReason);
        // The blockers: before payment, whoever cannot be paid yet; after, every line still unpaid.
        var pending = unpaid.Where(x => p.Period.Status == PeriodStatus.Paid || x.State != LinePaymentState.Ready)
            .Select(x => new PayrollReportRowDto([ReportCell.Text(x.Line.FullName), ReportCell.Money(x.Line.Entity.TotalNet),
                ReportCell.Text(x.State switch
                {
                    LinePaymentState.CardRequested => "Tarjeta en trámite",
                    LinePaymentState.MissingAccount => "Sin cuenta",
                    LinePaymentState.InvalidAccount => "Cuenta inválida",
                    LinePaymentState.Rejected => $"Rechazado: {reasons.GetValueOrDefault(x.Line.Entity.Uid)}",
                    _ => "Cuenta lista: va en el archivo de pendientes",
                })]))
            .ToList();
        var manualRows = lines.Where(x => x.Manual is not null).OrderBy(x => x.FullName)
            .Select(x => new PayrollReportRowDto([ReportCell.Text(x.FullName), ReportCell.Money(x.Manual!.Amount),
                ReportCell.Text(x.Manual.Method switch { ManualPaymentMethod.Cash => "Efectivo", ManualPaymentMethod.Check => "Cheque", _ => "Otro" }),
                ReportCell.Text(x.Manual.Reference), ReportCell.Text(Date(x.Manual.PaidOn))]))
            .ToList();
        return new(PayrollReport.Payments, title, r.Filters,
        [
            new("Resumen", ["Concepto", "Personas", "Importe"], summary),
            new("Por banco receptor y tipo de pago", ["Banco", "Tipo de pago", "Pagos", "Importe"], byBank),
            new("Pendientes y rechazados", ["Empleado", "Importe", "Motivo"], pending),
            new("Pagados fuera de archivo", ["Empleado", "Importe", "Forma", "Referencia", "Fecha"], manualRows),
        ], p.Period.Status == PeriodStatus.Paid ? null : "El período aún no se paga: aún no hay archivos.");
    }

    // ---- 3 · Incidencias del período ----

    private static async Task<PayrollReportDto> IncidentsAsync(DbContext db, Resolved r, CancellationToken ct)
    {
        var ids = Covered(r).Select(p => p.Period.Uid).ToList();
        var incidents = await PrepayrollQueries.Incidents(db, db.Set<Incident>().Where(i => ids.Contains(i.PayrollPeriodId)), ct);
        var deductions = new[] { PayConcept.Deduction.Info().Code, PayConcept.LoanInstallment.Info().Code };
        var hours = new[] { PayConcept.Overtime.Info().Code, PayConcept.OvertimeDouble.Info().Code, PayConcept.OvertimeTriple.Info().Code };
        // What each incidencia pays is its items in the line (horas extra are priced there from the hours), signed toward net pay.
        var incidentIds = incidents.Select(i => i.Id).ToList();
        var paid = (await db.Set<PrepayrollLineItem>().AsNoTracking().Where(x => x.IncidentId != null && incidentIds.Contains(x.IncidentId.Value))
                .Join(db.Set<PayConceptRow>(), x => x.PayConceptId, c => c.Uid, (x, c) => new { Id = x.IncidentId!.Value, x.Amount, c.Kind })
                .ToListAsync(ct))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Sum(x => x.Kind == ConceptKind.Deduction ? -x.Amount : x.Amount));
        var rows = new List<PayrollReportRowDto>();
        foreach (var g in incidents.GroupBy(i => i.AdjustedColumn is { } col ? $"Ajuste manual · {col}" : i.ConceptName).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var signed = g.Select(i => (I: i, Amount: PrepayrollCalculator.TruncateToCents(paid.TryGetValue(i.Id, out var a) ? a
                    : deductions.Contains(i.ConceptCode) ? -i.Amount : i.Amount)))
                .OrderBy(x => x.I.Date).ThenBy(x => x.I.FullName).ToList();
            rows.Add(new([ReportCell.Text(g.Key), ReportCell.Text($"{signed.Count} {(signed.Count == 1 ? "registro" : "registros")}"), ReportCell.Blank,
                ReportCell.Blank, ReportCell.Money(signed.Sum(x => x.Amount)), ReportCell.Blank, ReportCell.Blank, ReportCell.Blank, ReportCell.Blank,
                ReportCell.Blank], true));
            rows.AddRange(signed.Select(x => new PayrollReportRowDto(
            [
                ReportCell.Text(Date(x.I.Date)), ReportCell.Text(x.I.FullName), ReportCell.Text(g.Key),
                hours.Contains(x.I.ConceptCode) ? ReportCell.Text($"{x.I.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} h") : ReportCell.Blank,
                ReportCell.Money(x.Amount), ReportCell.Text(x.I.Reason), ReportCell.Text(x.I.ReportedBy),
                ReportCell.Text(x.I.AdjustedColumn is not null ? "Ajuste manual" : x.I.OriginPeriodNumber is not null ? "Retroactivo" : "Incidencia"),
                ReportCell.Text(x.I.OriginPeriodNumber is { } n ? $"Semana {n}" : null), ReportCell.Text($"Semana {x.I.PeriodNumber}"),
            ], PeriodId: x.I.PeriodId)));
        }
        return new(PayrollReport.Incidents, Title(PayrollReport.Incidents), r.Filters,
        [
            new(null, ["Fecha", "Empleado", "Concepto", "Cantidad", "Importe", "Motivo", "Capturó", "Origen", "Corresponde a", "Se paga en"], rows),
        ], "Un descuento o abono a préstamo resta; un ajuste manual es el cambio que hizo a su columna. Festivos, prima y descansos "
            + "vienen de la asistencia, no de incidencias.");
    }

    // ---- 4 · Asistencia por empleado ----

    private static readonly Dictionary<AttendanceStatus, string> Letter = new()
    {
        [AttendanceStatus.Present] = "P",
        [AttendanceStatus.Partial] = "I",
        [AttendanceStatus.Absent] = "F",
        [AttendanceStatus.RestDay] = "D",
        [AttendanceStatus.RestDayWorked] = "DL",
        [AttendanceStatus.Holiday] = "FE",
        [AttendanceStatus.HolidayWorked] = "FL",
        [AttendanceStatus.SickLeave] = "IN",
        [AttendanceStatus.LeavePaid] = "PG",
        [AttendanceStatus.LeaveUnpaid] = "PS",
        [AttendanceStatus.Vacation] = "V",
    };

    private static async Task<PayrollReportDto> AttendanceAsync(DbContext db, IQueryable<PayrollGroup> groups, Resolved r, CancellationToken ct)
    {
        var title = Title(PayrollReport.Attendance);
        if (r.Filters.From is not { } from || r.Filters.To is not { } to)
            return new(PayrollReport.Attendance, title, r.Filters, [], "No hay períodos con prenómina.");
        var scoped = groups.Where(g => r.Filters.ClientId == null || g.ClientId == r.Filters.ClientId);
        if (r.Period is { } p)
            scoped = scoped.Where(g => g.Uid == p.Group.Uid);
        var employees = await db.Set<Employee>().AsNoTracking()
            .Where(e => scoped.Any(g => g.Uid == e.PayrollGroupId) && e.HiredAt <= to && (e.TerminatedAt == null || e.TerminatedAt > from))
            .Join(db.Set<Person>(), e => e.PersonId, x => x.Uid, (e, x) => new { e.Uid, x.FullName, e.PayrollGroupId })
            .Join(db.Set<PayrollGroup>(), x => x.PayrollGroupId, g => g.Uid, (x, g) => new { x.Uid, x.FullName, g.ClientId })
            .Join(db.Set<Client>(), x => x.ClientId, c => c.Uid, (x, c) => new { x.Uid, x.FullName, Client = c.ShortName })
            .ToListAsync(ct);
        var ids = employees.Select(e => e.Uid).ToList();
        var days = (await db.Set<AttendanceRecord>().AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync(ct))
            .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.WorkDate).ToList());
        var holidays = (await db.Set<Holiday>().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync(ct)).ToHashSet();

        static ReportCell Days(decimal d) => d == decimal.Truncate(d) ? ReportCell.Count((int)d) : ReportCell.Average(d);
        var summary = employees.Select(e =>
            {
                var list = days.GetValueOrDefault(e.Uid) ?? [];
                var s = AttendanceSummaries.Summarize(list, holidays);
                int Count(Func<AttendanceRecord, bool> f) => list.Count(f);
                return (e.FullName, e.Client, Summary: s, Absent: Count(a => a.Status == AttendanceStatus.Absent && !a.IsAutoAbsent),
                    Auto: Count(a => a.Status == AttendanceStatus.Absent && a.IsAutoAbsent), Sick: Count(a => a.Status == AttendanceStatus.SickLeave),
                    Leave: Count(a => a.Status is AttendanceStatus.LeavePaid or AttendanceStatus.LeaveUnpaid),
                    Vacation: Count(a => a.Status == AttendanceStatus.Vacation), Captured: list.Count);
            })
            .Where(x => x.Captured > 0)
            .OrderByDescending(x => x.Absent + x.Auto).ThenBy(x => x.FullName, StringComparer.Ordinal).ToList();
        var rows = summary.Select(x => new PayrollReportRowDto(
            [
                ReportCell.Text(x.FullName), ReportCell.Text(x.Client), Days(x.Summary.DaysWorked), ReportCell.Count(x.Absent), ReportCell.Count(x.Auto),
                ReportCell.Count(x.Sick), ReportCell.Count(x.Leave), ReportCell.Count(x.Vacation), Days(x.Summary.PartialHours),
                Days(x.Summary.OvertimeHours), Days(x.Summary.HolidaysWorked),
            ]))
            .ToList();
        var tables = new List<PayrollReportTableDto>
        {
            new(null, ["Empleado", "Cliente", "Días laborados", "Faltas", "Faltas automáticas", "Incapacidad", "Permisos", "Vacaciones",
                "Horas incompletas", "Horas extra", "Festivos laborados"], rows),
        };
        // The day grid for up to a month: a letter per day, «F*» where the day closed without capture.
        if (to.DayNumber - from.DayNumber <= 31)
        {
            var dates = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays).ToList();
            var grid = summary.Select(x =>
                {
                    var e = employees.First(y => y.FullName == x.FullName && y.Client == x.Client);
                    var byDate = (days.GetValueOrDefault(e.Uid) ?? []).ToDictionary(a => a.WorkDate);
                    return new PayrollReportRowDto(
                    [
                        ReportCell.Text(x.FullName),
                        .. dates.Select(d => byDate.TryGetValue(d, out var a)
                            ? ReportCell.Text(a.IsAutoAbsent ? "F*" : Letter[a.Status])
                            : ReportCell.Blank),
                    ]);
                })
                .ToList();
            tables.Add(new("Por día", ["Empleado", .. dates.Select(d => $"{d.Day} {Months[d.Month - 1]} {d.ToString("ddd", Es).TrimEnd('.')}")], grid));
        }
        return new(PayrollReport.Attendance, title, r.Filters, tables,
            "Días laborados con la regla de la prenómina (D84). Por día: P presente, I incompleta, F falta, F* sin captura al cerrar, D descanso, "
            + "DL descanso laborado, FE festivo, FL festivo laborado, IN incapacidad, PG / PS permiso con / sin goce, V vacaciones; vacío = sin captura.");
    }

    // ---- 5 · Rotación ----

    private static async Task<PayrollReportDto> TurnoverAsync(DbContext db, IQueryable<PayrollGroup> groups, Resolved r, CancellationToken ct)
    {
        var from = r.Filters.From!.Value;
        var to = r.Filters.To!.Value;
        var scoped = groups.Where(g => r.Filters.ClientId == null || g.ClientId == r.Filters.ClientId);
        var employees = await db.Set<Employee>().AsNoTracking().Where(e => scoped.Any(g => g.Uid == e.PayrollGroupId) && e.HiredAt <= to)
            .Join(db.Set<Person>(), e => e.PersonId, x => x.Uid, (e, x) => new { e, x.FullName })
            .Join(db.Set<PayrollGroup>(), x => x.e.PayrollGroupId, g => g.Uid, (x, g) => new { x.e, x.FullName, Group = g.Name, g.ClientId })
            .Join(db.Set<Client>(), x => x.ClientId, c => c.Uid, (x, c) => new { x.e, x.FullName, x.Group, Client = c.ShortName })
            .Join(db.Set<Position>(), x => x.e.PositionId, p => p.Uid, (x, p) => new { x.e, x.FullName, x.Group, x.Client, Position = p.Name })
            .ToListAsync(ct);
        var ids = employees.Select(x => x.e.Uid).ToList();
        var reasons = (await db.Set<EmployeeStatusPeriod>().AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId) && s.Status == EmployeeStatus.Terminated).ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartDate).Last().Reason);
        static bool Active(Employee e, DateOnly d) => e.HiredAt <= d && (e.TerminatedAt is not { } t || t > d);

        var rows = new List<PayrollReportRowDto>();
        var months = new List<DateOnly>();
        for (var m = from; m <= to; m = m.AddMonths(1))
            months.Add(m);
        foreach (var client in employees.GroupBy(x => x.Client).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var (hires, terms, early) = (0, 0, 0);
            foreach (var m in months)
            {
                var end = m.AddMonths(1).AddDays(-1);
                var hired = client.Count(x => x.e.HiredAt >= m && x.e.HiredAt <= end);
                var left = client.Where(x => x.e.TerminatedAt is { } t && t >= m && t <= end).ToList();
                var quick = left.Count(x => x.e.TerminatedAt!.Value.DayNumber - x.e.HiredAt.DayNumber <= 30);
                var atEnd = client.Where(x => Active(x.e, end)).ToList();
                rows.Add(new(
                [
                    ReportCell.Text(client.Key), ReportCell.Text(Month(m)), ReportCell.Count(client.Count(x => Active(x.e, m))), ReportCell.Count(hired),
                    ReportCell.Count(left.Count), ReportCell.Count(quick), ReportCell.Count(atEnd.Count),
                    ReportCell.Average(atEnd.Count == 0 ? null : (decimal)atEnd.Average(x => end.DayNumber - x.e.HiredAt.DayNumber)),
                ]));
                (hires, terms, early) = (hires + hired, terms + left.Count, early + quick);
            }
            rows.Add(new([ReportCell.Text(client.Key), ReportCell.Text("Total"), ReportCell.Blank, ReportCell.Count(hires), ReportCell.Count(terms),
                ReportCell.Count(early), ReportCell.Blank, ReportCell.Blank], true));
        }
        var inRange = employees.Where(x => x.e.TerminatedAt is { } t && t >= from && t <= to).ToList();
        var byReason = inRange.GroupBy(x => reasons.GetValueOrDefault(x.e.Uid) ?? "Sin motivo").OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new PayrollReportRowDto([ReportCell.Text(g.Key), ReportCell.Count(g.Count()),
                ReportCell.Percent(inRange.Count == 0 ? null : (decimal)g.Count() / inRange.Count)]))
            .ToList();
        var moves = employees.Where(x => x.e.HiredAt >= from && x.e.HiredAt <= to).Select(x => (Date: x.e.HiredAt, Kind: "Alta", x, Reason: (string?)null))
            .Concat(inRange.Select(x => (Date: x.e.TerminatedAt!.Value, Kind: "Baja", x, Reason: reasons.GetValueOrDefault(x.e.Uid))))
            .OrderBy(m => m.Date).ThenBy(m => m.x.FullName, StringComparer.Ordinal)
            .Select(m => new PayrollReportRowDto([ReportCell.Text(Date(m.Date)), ReportCell.Text(m.Kind), ReportCell.Text(m.x.FullName),
                ReportCell.Text($"{m.x.Client} · {m.x.Group}"), ReportCell.Text(m.x.Position), ReportCell.Text(m.x.e.CandidateId is null ? "Directo" : "Vacante"),
                ReportCell.Text(m.Reason)]))
            .ToList();
        return new(PayrollReport.Turnover, Title(PayrollReport.Turnover), r.Filters,
        [
            new("Por cliente y mes", ["Cliente", "Mes", "Plantilla al inicio", "Altas", "Bajas", "Bajas en 30 días o menos", "Plantilla al cierre",
                "Antigüedad promedio (días)"], rows),
            new("Motivos de baja", ["Motivo", "Bajas", "%"], byReason),
            new("Altas y bajas", ["Fecha", "Movimiento", "Empleado", "Cliente · grupo", "Puesto", "Origen", "Motivo"], moves),
        ], "Un reingreso cuenta como alta nueva. Origen «Vacante»: convertido desde un candidato contratado.");
    }

    // ---- 6 · Tarjetas y cuentas ----

    private static async Task<PayrollReportDto> AccountsAsync(DbContext db, IQueryable<PayrollGroup> groups, Resolved r, DateOnly today, CancellationToken ct)
    {
        var scoped = groups.Where(g => r.Filters.ClientId == null || g.ClientId == r.Filters.ClientId);
        var employees = await db.Set<Employee>().AsNoTracking()
            .Where(e => scoped.Any(g => g.Uid == e.PayrollGroupId) && e.HiredAt <= today && (e.TerminatedAt == null || e.TerminatedAt > today))
            .Join(db.Set<Person>(), e => e.PersonId, x => x.Uid, (e, x) => new { e.Uid, e.PersonId, x.FullName, e.PayrollGroupId })
            .Join(db.Set<PayrollGroup>(), x => x.PayrollGroupId, g => g.Uid, (x, g) => new { x.Uid, x.PersonId, x.FullName, g.ClientId })
            .Join(db.Set<Client>(), x => x.ClientId, c => c.Uid, (x, c) => new { x.Uid, x.PersonId, x.FullName, Client = c.ShortName })
            .ToListAsync(ct);
        var people = employees.Select(e => e.PersonId).ToList();
        var ids = employees.Select(e => e.Uid).ToList();
        var accounts = (await db.Set<PersonBankAccount>().AsNoTracking()
                .Where(a => people.Contains(a.PersonId) && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today)).ToListAsync(ct))
            .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.First());
        var banks = await db.Set<BankRow>().AsNoTracking().ToDictionaryAsync(b => b.Uid, b => b.Name, ct);
        var requests = (await db.Set<CardRequest>().AsNoTracking()
                .Where(c => ids.Contains(c.EmployeeId)
                    && (c.Status == CardRequestStatus.Queued || c.Status == CardRequestStatus.InBatch || c.Status == CardRequestStatus.Uploaded))
                .ToListAsync(ct))
            .ToDictionary(c => c.EmployeeId);

        var detail = new List<PayrollReportRowDto>();
        var (missing, cards, third, unregistered) = (0, 0, 0, 0);
        foreach (var e in employees.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            var a = accounts.GetValueOrDefault(e.PersonId);
            var request = requests.GetValueOrDefault(e.Uid);
            var situations = new List<string>();
            if (a is null && request is null)
            {
                situations.Add("Sin cuenta");
                missing++;
            }
            if (request is not null)
            {
                situations.Add($"Tarjeta {(request.Bank == RegistrationBank.Banorte ? "Banorte" : "Banregio")} {RequestStatus(request.Status)}");
                cards++;
            }
            if (a is not null && !BankAccounts.SameName(a.HolderName, e.FullName))
            {
                situations.Add($"A nombre de {a.HolderName}");
                third++;
            }
            if (a is not null && !a.IsFrequentAccountRegistered)
            {
                situations.Add("No registrada como cuenta frecuente");
                unregistered++;
            }
            if (situations.Count > 0)
                detail.Add(new([ReportCell.Text(e.FullName), ReportCell.Text(e.Client), ReportCell.Text(string.Join(" · ", situations)),
                    ReportCell.Text(a is null ? null : banks[a.BankId]), ReportCell.Text(a is null ? null : $"·{a.AccountLast4}")]));
        }
        var summary = new List<PayrollReportRowDto>
        {
            new([ReportCell.Text("Empleados activos"), ReportCell.Count(employees.Count)], true),
            new([ReportCell.Text("Sin cuenta ni tarjeta en trámite"), ReportCell.Count(missing)]),
            new([ReportCell.Text("Con tarjeta en trámite"), ReportCell.Count(cards)]),
            new([ReportCell.Text("Pagados a cuenta de un tercero"), ReportCell.Count(third)]),
            new([ReportCell.Text("Cuentas no registradas como frecuentes"), ReportCell.Count(unregistered)]),
        };
        var byBank = requests.Values.GroupBy(c => c.Bank).OrderBy(g => g.Key)
            .Select(g => new PayrollReportRowDto([ReportCell.Text(g.Key == RegistrationBank.Banorte ? "Banorte" : "Banregio"),
                ReportCell.Count(g.Count(c => c.Status == CardRequestStatus.Queued)), ReportCell.Count(g.Count(c => c.Status == CardRequestStatus.InBatch)),
                ReportCell.Count(g.Count(c => c.Status == CardRequestStatus.Uploaded)), ReportCell.Count(g.Count())]))
            .ToList();
        return new(PayrollReport.Accounts, Title(PayrollReport.Accounts), r.Filters,
        [
            new("Resumen", ["Situación", "Empleados"], summary),
            new("Solicitudes de tarjeta por banco", ["Banco", "En espera", "En archivo de alta", "Subidas al banco", "Total"], byBank),
            new("Detalle", ["Empleado", "Cliente", "Situación", "Banco", "Cuenta"], detail),
        ], "Empleados activos hoy; la cuenta es la principal vigente de cada persona.");
    }

    private static string RequestStatus(CardRequestStatus s) => s switch
    {
        CardRequestStatus.Queued => "en espera",
        CardRequestStatus.InBatch => "en archivo de alta",
        _ => "subida al banco",
    };

    /// <summary>The report as a document for the Excel / PDF renderer: the same tables and cells.</summary>
    public static ReportDocument Document(PayrollReportDto dto) =>
        new(dto.Title, dto.Filters.Description,
            dto.Tables.Select(t => new ReportTable(t.Heading, t.Columns, t.Rows.Select(r => new ReportRow(r.Cells, r.Emphasis)).ToList())).ToList());
}
