// SEM 38 of the client's workbook (examples/PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx) as seed data: the 115 rows as
// people and employees, and each row's week as attendance that gives the row back under PLAN D84 — the same layout as
// PrepayrollTests.Week, which the golden test checks. Bank numbers in COMENTARIOS are never kept: only the bank's name.
import { readSheet } from './xlsx.mjs';

export const SEM38_FILE = 'examples/PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx';
export const SEM38 = { start: '2026-09-11', end: '2026-09-17', sunday: '2026-09-13', holiday: '2026-09-16', number: 38 };
export const GOLDEN = { totalNet: '334654.83', totalNetExact: '334655.149146', rows: 115 };

const BANKS = [['BANCO AZTECA', '127'], ['AZTECA', '127'], ['BBVA', '012'], ['BANORTE', '072'], ['BANAMEX', '002'], ['SANTANDER', '014'], ['BANREGIO', '058']];

const addDays = (iso, n) => {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};
const fromSerial = (serial) => addDays('1899-12-30', +serial);
const hours = (cell, row) => {
  if (!cell?.formula) return 0;
  const m = cell.formula.match(/^G(\d+)\*(\d+(?:\.\d+)?)$/);
  if (!m || +m[1] !== row) throw new Error(`SEM 38 row ${row}: unexpected formula ${cell.formula}`);
  return +m[2];
};

/**
 * COMENTARIOS without bank data: parts split on «/»; a part with a bank's name or a long number goes (its bank is kept apart);
 * notes stay («AGREGAR PRIMA», «EXTRANJERO», «PENSION DEL 39%», «CAMBIO DE CUENTA BANORTE» → «CAMBIO DE CUENTA»).
 */
export function splitComments(raw) {
  let bank = null;
  const kept = [];
  for (const part of (raw ?? '').split('/').map((p) => p.trim()).filter(Boolean)) {
    const named = BANKS.find(([name]) => part.toUpperCase().includes(name));
    if (named) bank ??= named[1];
    const withoutNumbers = part.replace(/\d[\d\s-]{9,}\d/g, '').trim();
    const withoutBank = named ? withoutNumbers.replace(new RegExp(named[0], 'i'), '').trim() : withoutNumbers;
    if (withoutBank && !/^\d[\d\s-]*$/.test(withoutBank)) kept.push(withoutBank);
  }
  return { comments: kept.length ? kept.join(' / ') : null, bank };
}

/** The 115 rows: name (paterno materno nombres), puesto, turno, hire date, rates, the week's figures, comments and bank. */
export function sem38Rows(path = SEM38_FILE) {
  const cells = readSheet(path, 'SEM 38');
  const text = (ref) => (cells.get(ref)?.value ?? '').trim();
  const num = (ref) => +(cells.get(ref)?.value ?? 0);
  const rows = [];
  for (let r = 6; r <= 120; r++) {
    const words = text(`C${r}`).split(/\s+/);
    if (words.length < 3) throw new Error(`SEM 38 row ${r}: name «${text(`C${r}`)}» has fewer than three words`);
    const { comments, bank } = splitComments(cells.get(`W${r}`)?.value);
    rows.push({
      row: r,
      lastNamePaternal: words[0],
      lastNameMaternal: words[1],
      firstNames: words.slice(2).join(' '),
      fullName: words.join(' '),
      puesto: text(`D${r}`),
      turno: text(`E${r}`),
      hiredAt: fromSerial(num(`B${r}`)),
      rates: { pocket: num(`J${r}`), imss: num(`I${r}`), hourly: num(`G${r}`) },
      days: num(`F${r}`),
      sunday: num(`M${r}`) !== 0,
      partialHours: hours(cells.get(`N${r}`), r),
      holiday: num(`O${r}`) !== 0,
      overtimeHours: hours(cells.get(`P${r}`), r),
      restDayWorked: num(`R${r}`) !== 0,
      comments,
      bank,
      foreign: /EXTRANJERO/i.test(cells.get(`W${r}`)?.value ?? ''),
    });
  }
  // Rows are matched to employees by name: a repeated name would mix two people's weeks.
  if (new Set(rows.map((x) => x.fullName)).size !== rows.length) throw new Error('SEM 38: two rows have the same name');
  return rows;
}

/**
 * The row's week (D84): Festivo on the 16th when O (a paid día), the Sunday worked when M and a descanso otherwise, a
 * descanso laborado when R, Presente for the rest of F, one Incompleta of N's hours, P's hours as extra on the first worked
 * day. Days left over stay uncaptured: faltas when the period closes. Throws when the row does not fit.
 */
export function sem38Week(r) {
  const days = [];
  for (let d = r.hiredAt > SEM38.start ? r.hiredAt : SEM38.start; d <= SEM38.end; d = addDays(d, 1)) days.push(d);
  const out = [];
  const add = (date, status, hoursWorked = null) => { const x = { date, status, hoursWorked, overtimeHours: null }; out.push(x); return x; };
  let open = [...days];
  let dias = r.days;
  if (r.holiday) {
    add(SEM38.holiday, 'holiday');
    open = open.filter((d) => d !== SEM38.holiday);
    dias--;
  }
  const worked = [];
  if (r.restDayWorked) worked.push(['rest_day_worked', null]);
  while (worked.length < dias) worked.push(['present', null]);
  if (r.partialHours > 0) worked.push(['partial', String(r.partialHours)]);
  const slots = open.filter((d) => d !== SEM38.sunday);
  if (open.includes(SEM38.sunday)) {
    if (r.sunday) slots.unshift(SEM38.sunday);
    else add(SEM38.sunday, 'rest_day');
  }
  if (worked.length > slots.length) throw new Error(`SEM 38 row ${r.row}: ${worked.length} worked days do not fit in ${slots.length}`);
  worked.forEach(([status, h], i) => add(slots[i], status, h));
  if (r.overtimeHours > 0) out.find((x) => x.status === 'present' || x.status === 'rest_day_worked').overtimeHours = String(r.overtimeHours);
  return out.sort((a, b) => a.date.localeCompare(b.date));
}
