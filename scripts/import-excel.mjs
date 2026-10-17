#!/usr/bin/env node
// One-time import of the client's workbooks into a NEW installation (DATA_MODEL §10, PLAN D90), through the API so every
// derived figure is the platform's own. Run by a human after `scripts/bootstrap.mjs`:
//
//   API_URL=https://api.example.com IMPORT_TOKEN=<owner's ID token> node scripts/import-excel.mjs --dir ./excel --out ./import.md
//
// Locally (Auth emulator) it signs in as IMPORT_EMAIL / IMPORT_PASSWORD (default: the seed's owner). With MFA on, pass the
// current code in IMPORT_MFA_CODE. It refuses an installation that already has payroll groups or employees.
//
// What it loads: the clients of CATALOGOS; ECOVIA «VALIDADORES 12 HRS» from PRENOMINA SEM 38 — puestos at the rates most rows
// carry, the 115 people and employees (other rates as overrides, BAJA / INCAPACIDAD as status, the rest of COMENTARIOS as
// notes, the account each COMENTARIOS names as the principal account); the cuenta de cargo of the HFC master; then Semana 38
// as attendance, closed, and its grid compared row by row, column by column, with the sheet (K–V and V121).
// What it reports and does not load: vacantes and candidatos rows (captured in the app, D90), recruiters (they need an email to
// be invited), and bank-file rows that match no imported person. The report prints names and last four digits only — never
// an account number — and is personal data: keep it private.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { loadEnv } from './lib/env.mjs';
import { accountsFromComments, differs, masked, nameKey, positionRates, shown, statusFromComments } from './lib/import.mjs';
import { SEM38, sem38Rows, sem38Week, splitComments } from './lib/sem38.mjs';
import { readSheet } from './lib/xlsx.mjs';

const { values: args } = parseArgs({
  options: {
    dir: { type: 'string', default: 'examples' },
    out: { type: 'string' },
    force: { type: 'boolean', default: false },
    // Someone without an account in COMENTARIOS is queued for a card here (an alta needs one or the other, D82).
    'card-bank': { type: 'string', default: 'banorte' },
  },
});
const env = loadEnv();
const API = env.API_URL.replace(/\/$/, '');
const FILES = {
  recruiting: 'Sistema_Reclutamiento_Completo_2026.xlsx',
  sem38: 'PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx',
  banorte: 'Alta_Nomina_Masiva Banorte 21Ago-1.xlsx',
  banregio: 'ALTA BANREGIO EARL 19AGO26.xlsx',
  master: 'Lo HFC MAESTRO 14AGOSTO.xlsx',
  frequent: 'cuentasfrec21sept.xlsx',
};
const file = (key) => join(args.dir, FILES[key]);
const report = { loaded: [], review: [], errors: [], diff: [] };
const addDays = (iso, n) => {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};

// ---- Sign-in ----
async function token() {
  if (env.IMPORT_TOKEN) return env.IMPORT_TOKEN;
  const host = env.FIREBASE_AUTH_EMULATOR_HOST;
  if (!host) throw new Error('Set IMPORT_TOKEN (the owner\'s ID token) or run against the Auth emulator.');
  const response = await fetch(`http://${host}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=demo-api-key`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: env.IMPORT_EMAIL ?? 'edgar@local.test', password: env.IMPORT_PASSWORD ?? 'plataforma-local', returnSecureToken: true }),
  });
  const body = await response.json();
  if (!response.ok) throw new Error(`Sign-in failed: ${body?.error?.message ?? response.status}`);
  return body.idToken;
}
const bearer = await token();

async function api(method, path, payload) {
  const response = await fetch(`${API}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${bearer}` },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  });
  const body = await response.json().catch(() => ({}));
  return { ok: response.ok, status: response.status, body };
}
const problem = (r) => r.body?.detail ?? Object.values(r.body?.errors ?? {}).flat().join(' ') ?? `HTTP ${r.status}`;
async function must(method, path, payload) {
  const r = await api(method, path, payload);
  if (!r.ok) throw new Error(`${method} ${path}: ${problem(r)}`);
  return r.body;
}

// ---- Preconditions ----
let me = await must('GET', '/v1/me');
if (me.mfaEnabled && !me.mfaVerified) {
  if (!env.IMPORT_MFA_CODE) throw new Error('The owner has two-step verification on: pass the current code in IMPORT_MFA_CODE.');
  await must('POST', '/v1/me/mfa/verify', { code: env.IMPORT_MFA_CODE });
  me = await must('GET', '/v1/me');
}
if (me.role !== 'administrator') throw new Error('The import runs as the owner (an Administrador with every grant).');
const groups = await must('GET', '/v1/payroll/groups');
const existing = await must('GET', '/v1/employees?includeTerminated=true');
if ((groups.length || existing.length) && !args.force) {
  throw new Error(`This installation already has ${groups.length} payroll groups and ${existing.length} employees: the import is for a new one.`);
}

// ---- CATALOGOS: clients (recruiters reported) ----
const catalog = readSheet(file('recruiting'), 'CATALOGOS');
const column = (letter) => {
  const out = [];
  for (let r = 2; r < 200; r++) {
    const v = catalog.get(`${letter}${r}`)?.value?.trim();
    if (v) out.push(v);
  }
  return out;
};
const clientIds = Object.fromEntries((await must('GET', '/v1/clients')).map((c) => [c.shortName.toUpperCase(), c.id]));
let createdClients = 0;
for (const name of new Set([...column('E').map((c) => c.toUpperCase()), 'ECOVIA'])) {
  if (clientIds[name]) continue;
  const r = await api('POST', '/v1/clients', { shortName: name, legalName: null, rfc: null, primaryContact: null });
  if (r.ok) {
    clientIds[name] = r.body.id;
    createdClients++;
  } else report.errors.push(`Cliente ${name}: ${problem(r)}`);
}
report.loaded.push(`Clientes: ${createdClients} creados (${Object.keys(clientIds).length} en total).`);
const users = await must('GET', '/v1/users');
const recruiters = column('D').filter((n) => !users.some((u) => nameKey(u.displayName) === nameKey(n)));
if (recruiters.length) report.review.push(`Reclutadores sin usuario (invítalos en Usuarios con su correo): ${recruiters.join(', ')}.`);
for (const [sheet, label] of [['BD_VACANTES', 'vacantes'], ['BD_CANDIDATOS', 'candidatos']]) {
  const cells = readSheet(file('recruiting'), sheet);
  let rows = 0;
  for (let r = 2; r < 5000; r++) if (cells.get(`A${r}`)?.value?.trim() || cells.get(`C${r}`)?.value?.trim()) rows++;
  report.loaded.push(`${sheet}: ${rows} filas${rows ? ` — no se importan: las ${label} se capturan en la plataforma (D90)` : ''}.`);
  if (rows) report.review.push(`${rows} ${label} de ${sheet} por capturar en la plataforma.`);
}

// ---- PRENOMINA SEM 38: group, puestos, people, employees ----
const rows = sem38Rows(file('sem38'));
const banks = Object.fromEntries((await must('GET', '/v1/catalogs')).banks.map((b) => [b.speiCode, b.id]));
const header = readSheet(file('sem38'), 'SEM 38');
const group = await must('POST', '/v1/payroll/groups', {
  clientId: clientIds.ECOVIA, name: 'VALIDADORES 12 HRS', periodType: 'weekly', cutoffWeekday: 4, shiftHours: '12', attendanceLockTime: '23:59:00',
  seventhDayDivisor: 6, sundayPremiumPct: '0.25', holidayWorkedMultiplier: '2',
  scheduleDescription: header.get('F3')?.value?.trim() || null, timeZone: null, isActive: true,
});
const rates = positionRates(rows);
const firstHire = rows.map((r) => r.hiredAt).sort()[0];
const positionIds = {};
for (const [puesto, r] of rates) {
  const after = await must('POST', `/v1/payroll/groups/${group.id}/positions`, {
    name: puesto, dailySalaryPocket: r.pocket.toFixed(2), dailySalaryImss: r.imss.toFixed(2), hourlyRate: String(r.hourly), daysWorkedBonus: '0',
    validFrom: firstHire,
  });
  positionIds[puesto] = after.positions.find((p) => p.name === puesto).id;
}
report.loaded.push(`Grupo ECOVIA · VALIDADORES 12 HRS con ${rates.size} puestos (${[...rates].map(([p, r]) => `${p} $${r.pocket}`).join(', ')}).`);

const raw = (r) => header.get(`W${r}`)?.value ?? '';
const employees = {};
let accounts = 0;
for (const r of rows) {
  const person = await must('POST', '/v1/people', {
    firstNames: r.firstNames, lastNamePaternal: r.lastNamePaternal, lastNameMaternal: r.lastNameMaternal, birthDate: null, birthState: null,
    gender: 'unspecified', isForeign: r.foreign, curp: null, rfc: null, nss: null, mobilePhone: null, email: null, street: null, extNumber: null,
    neighborhood: null, postalCode: null, municipality: null, state: null, source: null, notes: null,
  });
  const own = rates.get(r.puesto);
  const overrides = own.pocket !== r.rates.pocket || own.imss !== r.rates.imss || own.hourly !== r.rates.hourly;
  const found = accountsFromComments(raw(r.row));
  const usable = found.find((a) => a.type && banks[a.bankCode]);
  for (const a of found.filter((x) => x !== usable)) report.review.push(`${r.fullName}: cuenta ${masked(a.last4)} en COMENTARIOS sin tipo o banco reconocible.`);
  const body = {
    personId: person.id, payrollGroupId: group.id, positionId: positionIds[r.puesto], shift: r.turno, hiredAt: r.hiredAt,
    dailySalaryPocketOverride: overrides ? r.rates.pocket.toFixed(2) : null, dailySalaryImssOverride: overrides ? r.rates.imss.toFixed(2) : null,
    hourlyRateOverride: overrides ? String(r.rates.hourly) : null, daysWorkedBonusOverride: null,
    notes: splitComments(raw(r.row)).comments, cardBank: usable ? null : args['card-bank'], candidateId: null,
    bankAccount: usable ? {
      bankId: banks[usable.bankCode], accountType: usable.type, accountNumber: usable.digits, holderName: null, alias: null, validFrom: r.hiredAt,
      makePrimary: true,
    } : null,
  };
  let created = await api('POST', '/v1/employees', body);
  if (!created.ok && usable) {
    // The account failed the checks (length, CLABE digit, Luhn): the employee goes in without it, for review.
    report.review.push(`${r.fullName}: la cuenta ${masked(usable.last4)} no pasó la validación (${problem(created)}); captúrala en Personal.`);
    created = await api('POST', '/v1/employees', { ...body, bankAccount: null, cardBank: args['card-bank'] });
  } else if (created.ok && usable) accounts++;
  if (!created.ok) {
    report.errors.push(`${r.fullName}: ${problem(created)}`);
    continue;
  }
  employees[r.fullName] = created.body.id;
  if (!usable) report.review.push(`${r.fullName}: sin cuenta en COMENTARIOS — queda en Tarjetas por solicitar (${args['card-bank']}); si ya tiene cuenta, captúrala en Personal y la solicitud se cancela.`);
}
report.loaded.push(`Empleados: ${Object.keys(employees).length} de ${rows.length}, ${accounts} con cuenta principal tomada de COMENTARIOS.`);

// ---- Semana 38 as attendance, then the statuses that follow it ----
// The LFT holidays of the year (16 de septiembre is SEM 38's festivo); adding them twice changes nothing.
await api('POST', '/v1/catalogs/holidays/statutory', { year: Number(SEM38.start.slice(0, 4)) });
const writes = rows.flatMap((r) => (employees[r.fullName] ? sem38Week(r).map((d) => ({ employeeId: employees[r.fullName], ...d })) : []));
let next = 0;
const refused = [];
await Promise.all(Array.from({ length: 8 }, async () => {
  while (next < writes.length) {
    const w = writes[next++];
    const r = await api('POST', '/v1/attendance/unlock', {
      employeeId: w.employeeId, date: w.date, siteId: null, status: w.status, hoursWorked: w.hoursWorked, overtimeHours: w.overtimeHours, note: null,
      reason: 'Importación SEM 38',
    });
    if (!r.ok) refused.push(`${w.date} ${w.status}: ${problem(r)}`);
  }
}));
if (refused.length) report.errors.push(`${refused.length} días de asistencia rechazados, p. ej. ${refused.slice(0, 3).join('; ')}`);
const period = await must('POST', `/v1/payroll/groups/${group.id}/periods`, { date: SEM38.start });
for (const r of rows) {
  const status = statusFromComments(raw(r.row));
  if (!status || !employees[r.fullName]) continue;
  // A baja after the week (still paid in it); an incapacidad over the week (it has no días in the sheet).
  const body = status === 'terminated'
    ? { status, startDate: addDays(SEM38.end, 1), endDate: null, reason: 'Otro', reference: null }
    : { status, startDate: SEM38.start, endDate: SEM38.end, reason: null, reference: null };
  const s = await api('POST', `/v1/employees/${employees[r.fullName]}/status`, body);
  if (!s.ok) report.review.push(`${r.fullName}: ${status === 'terminated' ? 'baja' : 'incapacidad'} no registrada (${problem(s)}).`);
}
await must('POST', `/v1/payroll/periods/${period.id}/close-capture`);
let grid = await must('GET', `/v1/payroll/periods/${period.id}`);
const lines = Object.fromEntries(grid.rows.map((x) => [x.employeeId, x.lineId]));
for (const r of rows.filter((x) => x.comments && employees[x.fullName])) {
  await api('PUT', `/v1/payroll/lines/${lines[employees[r.fullName]]}/comments`, { comments: r.comments });
}
grid = await must('GET', `/v1/payroll/periods/${period.id}`);

// ---- The diff against the sheet: every column K–V of every row, and V121 ----
const letters = 'KLMNOPQRSTU'.split('');
const sheetColumn = Object.fromEntries(letters.map((l) => [header.get(`${l}5`)?.value?.trim().toUpperCase(), l]));
const byEmployee = Object.fromEntries(grid.rows.map((x) => [x.employeeId, x]));
for (const r of rows) {
  const line = byEmployee[employees[r.fullName]];
  if (!line) {
    report.diff.push(`| ${r.row} | ${r.fullName} | sin línea | | |`);
    continue;
  }
  for (const [i, col] of grid.columns.entries()) {
    const letter = sheetColumn[col.toUpperCase()];
    if (!letter) continue;
    const sheet = header.get(`${letter}${r.row}`)?.value ?? 0;
    if (differs(line.amounts[i], sheet)) report.diff.push(`| ${r.row} | ${r.fullName} | ${col} | ${shown(sheet)} | ${shown(line.amounts[i])} |`);
  }
  const total = header.get(`V${r.row}`)?.value ?? 0;
  if (differs(line.totalNet, total)) report.diff.push(`| ${r.row} | ${r.fullName} | TOTAL | ${shown(total)} | ${shown(line.totalNet)} |`);
}
const v121 = Number(header.get('V121')?.value ?? 0);

// ---- Bank files: rows that match an imported person, the rest for review ----
const people = new Map(rows.map((r) => [nameKey(r.fullName), r.fullName]));
const banorte = readSheet(file('banorte'), 'Masiva_xls');
let banorteRows = 0;
for (let r = 3; r < 8000; r++) if (banorte.get(`D${r}`)?.value?.trim()) banorteRows++;
report.review.push(`Alta Banorte: ${banorteRows} registros sin nombre (solo CURP); ninguno se puede ligar a un empleado importado — se dan de alta en Personal cuando lleguen.`);
const banregio = readSheet(file('banregio'), 'Hoja1');
const banregioNames = [];
for (let r = 2; r < 500; r++) {
  const name = ['B', 'C', 'D'].map((l) => banregio.get(`${l}${r}`)?.value ?? '').join(' ').trim();
  if (name) banregioNames.push(name);
}
const banregioMatched = banregioNames.filter((n) => people.has(nameKey(n)));
report.review.push(`Alta Banregio: ${banregioNames.length} personas, ${banregioMatched.length} coinciden con un empleado importado${banregioMatched.length ? ` (${banregioMatched.join(', ')})` : ''}; las demás no son de ECOVIA.`);
const master = readSheet(file('master'), 'Hoja1');
let masterRows = 0;
const masterMatched = [];
for (let r = 2; r < 1000; r++) {
  const name = master.get(`J${r}`)?.value?.trim();
  if (!name) continue;
  masterRows++;
  if (people.has(nameKey(name))) masterMatched.push(name);
}
report.review.push(`Maestro HFC: ${masterRows} beneficiarios, ${masterMatched.length} coinciden con un empleado importado; los demás son de otros clientes o proveedores (fuera de alcance, D25).`);
const frequent = readSheet(file('frequent'), 'Hoja1');
const frequentNames = [1, 2, 3, 4, 5].map((r) => frequent.get(`B${r}`)?.value?.trim()).filter(Boolean);
report.review.push(`Cuentas frecuentes: ${frequentNames.length} filas, ${frequentNames.filter((n) => people.has(nameKey(n))).length} de empleados importados.`);

// ---- The cuenta de cargo (HFC master's first row) ----
const charge = master.get('A2')?.value?.replace(/\D/g, '');
if (charge) {
  const pay = await api('PUT', '/v1/payroll/paying-account', {
    name: 'Cuenta Azteca', chargeAccountNumber: charge, holderName: master.get('B2')?.value?.trim(), holderRfc: master.get('C2')?.value?.trim(),
    chargeLegend: master.get('D2')?.value?.trim() || 'PAGO', creditLegend: master.get('K2')?.value?.trim() || 'PAGO',
  });
  report[pay.ok ? 'loaded' : 'errors'].push(pay.ok ? `Cuenta de cargo ${masked(pay.body.last4)} (${pay.body.holderName}).` : `Cuenta de cargo: ${problem(pay)}`);
}

// ---- Report ----
const out = args.out ?? `import-report-${new Date().toISOString().slice(0, 10)}.md`;
const text = `# Importación de los Excel — ${new Date().toISOString().slice(0, 16).replace('T', ' ')} UTC

Contiene nombres de personas: es información personal, guárdala en privado. Ningún número de cuenta aparece aquí.

## Cargado
${report.loaded.map((x) => `- ${x}`).join('\n')}

## Semana 38 contra el Excel
- Líneas: ${grid.rows.length} (Excel: ${rows.length}).
- Total neto a pagar de la plataforma: ${Number(grid.totalNet).toFixed(2)} (exacto ${grid.totalNetExact}); V121 del Excel: ${v121.toFixed(2)} (exacto ${v121}).
- Diferencias de al menos un centavo por celda (${report.diff.length}):

| Fila | Empleado | Columna | Excel | Plataforma |
|---|---|---|---|---|
${report.diff.join('\n')}

## Para revisar a mano (${report.review.length})
${report.review.map((x) => `- ${x}`).join('\n')}

## Errores (${report.errors.length})
${report.errors.map((x) => `- ${x}`).join('\n') || '- ninguno'}
`;
writeFileSync(out, text);
console.log(`Importación terminada: ${Object.keys(employees).length} empleados, Semana 38 ${Number(grid.totalNet).toFixed(2)} (V121 ${v121.toFixed(2)}), ${report.diff.length} celdas distintas, ${report.review.length} para revisar, ${report.errors.length} errores. Reporte: ${out}`);
if (report.errors.length) process.exitCode = 1;
