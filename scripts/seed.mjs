#!/usr/bin/env node
// npm run seed — local sample data, created THROUGH THE API so derived data is computed by the real code.
// Refuses a non-local database. Grows with each slice of docs/PLAN.md, and is safe to re-run:
//   S0  the local owner, signed up in the Auth emulator with a known password and bootstrapped as the first Administrador;
//   S3  this year's and next year's LFT holidays, the recruiting puestos, the 11 clients of design-reference/data.js
//       (razón social, RFC, contacto principal, one site with its zona) and the 6 recruiters (same local password);
//   S4  the 13 vacantes and 30 candidatos of data.js at their etapas (HEB has two open CAJERO vacantes, so the date suffix
//       shows), two hires that fill GTS, the paused / cancelled vacantes, and the two sample notes. Skipped once any
//       vacante exists. Candidate emails are rewritten to @local.test (capture emails an invitation since S5);
//   S5  a Cliente user for HEB (heb@local.test) for the Clients app;
//   S7  the payroll groups and puestos of data.js (ECOVIA validadores, HFC cuadrilla, HEB limpieza; every bonus inactive as SEM 38
//       pays). ECOVIA's employees are the 115 rows of the client's SEM 38 workbook (examples/; since S9), HFC's and HEB's the 12
//       of data.js: people, payment accounts (valid card / CLABE numbers generated — never the workbook's — at the bank its
//       comments name, three paid to a third party), card requests for two without an account, status periods relative to
//       today and after Semana 38 (two vacations, an incapacidad, two bajas) and work schedules. Skipped once any group exists;
//   S8  a field supervisor (Asistencia, ECOVIA only), the current periods, today's ECOVIA capture and yesterday's via unlock;
//   S9  Semana 38's attendance as the workbook implies (D84), written through the unlock path; periods 38 and 42 created; the
//       lock job closes them to Revisión; the comments of SEM 38 without bank data; and the gate: Semana 38's grid totals
//       334,654.83 payable and 334,655.149146 exact (D41), or the seed fails. Skipped once Semana 38 exists.
import { assertLocalDatabase, loadEnv } from './lib/env.mjs';
import { GOLDEN, SEM38, sem38Rows, sem38Week } from './lib/sem38.mjs';
import { addDays, candidatos as sampleCandidates, clients as sampleClients, empleados as sampleEmployees, payrollGroups as samplePayrollGroups, puestos as samplePositions, RECRUITERS, vacantes as sampleOpenings } from '../design-reference/data.js';

export const LOCAL_OWNER = { email: 'edgar@local.test', password: 'plataforma-local', name: 'Edgar Rodríguez' };

const env = loadEnv();
assertLocalDatabase(env.DATABASE_URL);
const emulator = env.FIREBASE_AUTH_EMULATOR_HOST;
if (!emulator) throw new Error('FIREBASE_AUTH_EMULATOR_HOST is required for the local seed.');

async function signUpInEmulator({ email, password, name }) {
  const response = await fetch(
    `http://${emulator}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=demo-api-key`,
    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, displayName: name }) },
  );
  const body = await response.json();
  if (!response.ok && body?.error?.message !== 'EMAIL_EXISTS') throw new Error(`Emulator sign-up failed: ${JSON.stringify(body)}`);
}

async function internal(path, payload) {
  const response = await fetch(`${env.API_URL}/internal/${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Internal-Secret': env.INTERNAL_SECRET },
    body: JSON.stringify(payload),
  });
  return { status: response.status, body: await response.json().catch(() => ({})) };
}

async function signInInEmulator({ email, password }) {
  const response = await fetch(
    `http://${emulator}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=demo-api-key`,
    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, returnSecureToken: true }) },
  );
  const body = await response.json();
  if (!response.ok) throw new Error(`Emulator sign-in failed: ${JSON.stringify(body)}`);
  return body.idToken;
}

/** `/v1` as the owner; a 409 (already there) is fine on a re-run. */
async function api(token, method, path, payload) {
  const response = await fetch(`${env.API_URL}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok && response.status !== 409) throw new Error(`${method} ${path} failed (${response.status}): ${JSON.stringify(body)}`);
  return { status: response.status, body };
}

await signUpInEmulator(LOCAL_OWNER);
const result = await internal('bootstrap', { email: LOCAL_OWNER.email, displayName: LOCAL_OWNER.name });
if (result.status === 200) console.log(`owner created: ${LOCAL_OWNER.email} / ${LOCAL_OWNER.password}`);
else if (result.status === 409) console.log(`owner already exists: ${LOCAL_OWNER.email} / ${LOCAL_OWNER.password}`);
else throw new Error(`bootstrap failed (${result.status}): ${JSON.stringify(result.body)}`);

// ---- S3: catalogs, clients, recruiters ----
const token = await signInInEmulator(LOCAL_OWNER);

const year = new Date().getFullYear();
for (const y of [year, year + 1]) await api(token, 'POST', '/v1/catalogs/holidays/statutory', { year: y });

const puestos = [...new Set(sampleOpenings().map((v) => v.puesto))];
for (const nameEs of puestos) await api(token, 'POST', '/v1/catalogs/positions', { nameEs });

// The prototype's zone labels → the municipios of the catalog.
const ZONE_OF = { 'Monterrey centro': 'Monterrey', 'San Nicolás': 'San Nicolás de los Garza', Escobedo: 'General Escobedo' };
const zones = (await api(token, 'GET', '/v1/catalogs')).body.zones;
const zoneId = (label) => zones.find((z) => z.nameEs === (ZONE_OF[label] ?? label))?.id ?? null;

const existing = new Set((await api(token, 'GET', '/v1/clients')).body.map((c) => c.shortName));
let created = 0;
for (const c of sampleClients()) {
  if (existing.has(c.nombre)) continue;
  const contact = c.contactos[0];
  const client = await api(token, 'POST', '/v1/clients', {
    shortName: c.nombre,
    legalName: c.razonSocial,
    rfc: c.rfc,
    primaryContact: contact ? { fullName: contact.nombre, email: contact.correo, phone: null } : null,
  });
  for (const site of c.sitios) await api(token, 'POST', `/v1/clients/${client.body.id}/sites`, { name: site.nombre, zoneId: zoneId(c.zona) });
  created++;
}
console.log(`clients: ${created} created, ${existing.size} already there`);

const emails = new Set((await api(token, 'GET', '/v1/users')).body.map((u) => u.email));
const slug = (name) => name.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z]+/g, '.');
for (const name of RECRUITERS) {
  const email = `${slug(name)}@local.test`;
  if (emails.has(email)) continue;
  // Signed up first so the recruiter can sign in locally with the shared password; the invite reuses the account.
  await signUpInEmulator({ email, password: LOCAL_OWNER.password, name });
  await api(token, 'POST', '/v1/users', { email, displayName: name, role: 'recruiter', clientId: null, grants: null, clientContactId: null });
  console.log(`recruiter: ${email} / ${LOCAL_OWNER.password}`);
}

// ---- S4: vacantes, candidatos, notes ----
if ((await api(token, 'GET', '/v1/openings')).body.length > 0) {
  console.log('vacantes: already there, skipped');
} else {
  const catalogs = (await api(token, 'GET', '/v1/catalogs')).body;
  const clientIds = Object.fromEntries((await api(token, 'GET', '/v1/clients')).body.map((c) => [c.shortName, c.id]));
  const recruiterIds = Object.fromEntries((await api(token, 'GET', '/v1/recruiters')).body.map((r) => [r.displayName, r.id]));
  const bankIds = ['072', '127'].map((code) => catalogs.banks.find((b) => b.speiCode === code)?.id).filter(Boolean);
  const secundaria = catalogs.educationLevels.find((e) => /secundaria/i.test(e.nameEs))?.id ?? null;
  const PRIORITY = { Alta: 'high', Media: 'medium', Baja: 'low' };

  const openings = sampleOpenings();
  const ids = [];
  for (const v of openings) {
    const { body } = await api(token, 'POST', '/v1/openings', {
      clientId: clientIds[v.cliente],
      zoneId: zoneId(v.zona),
      positionName: v.puesto,
      headcountRequested: v.solicitadas,
      monthlySalary: v.sueldo.toFixed(2),
      benefits: v.prestaciones,
      schedule: v.horario,
      paymentBankIds: bankIds,
      educationLevelId: secundaria,
      yearsExperience: v.experiencia,
      requiredExperience: v.expReq,
      genderRequirement: 'unspecified',
      ageRange: v.edad,
      priority: PRIORITY[v.prioridad],
      leadRecruiterId: recruiterIds[v.reclutador] ?? null,
      documentTemplateId: null,
      visibleToClient: true,
      notes: null,
      requestedAt: v.abierta,
    });
    ids.push(body.id);
  }

  const STAGE = { Nuevo: 'new', Contactado: 'contacted', Entrevista: 'interview', Apto: 'suitable', 'No apto': 'not_suitable', Contratado: 'hired', Descartado: 'discarded' };
  const SOURCE = { Facebook: 'facebook', LinkedIn: 'linkedin', WhatsApp: 'whatsapp', Referido: 'referral', 'Bolsa de trabajo': 'job_board', Otro: 'other' };
  const REASON = { not_suitable: 'No aprobó la entrevista', discarded: 'No se presentó' };
  // The prototype's sample CURPs are not valid CURPs, so people are captured without one.
  const sample = sampleCandidates(openings).map((c) => ({ ...c, vacIndex: openings.findIndex((v) => v.id === c.vacId) }));
  // GTS is «Cubierta» in the prototype: two hires make the API derive it.
  sample.push(
    { nombres: 'Ernesto', apellidoP: 'Salinas', apellidoM: 'Rocha', telefono: '8123456781', correo: null, fechaNac: '1990-04-12', fuente: 'Referido', etapa: 'Contratado', vacIndex: 4, sueldo: 13500 },
    { nombres: 'Liliana', apellidoP: 'Cavazos', apellidoM: 'Elizondo', telefono: '8123456782', correo: null, fechaNac: '1994-07-21', fuente: 'Facebook', etapa: 'Contratado', vacIndex: 4, sueldo: 13500 },
  );
  const candidateIds = [];
  for (const [i, c] of sample.entries()) {
    const { body } = await api(token, 'POST', '/v1/candidates', {
      personId: null,
      person: {
        firstNames: c.nombres,
        lastNamePaternal: c.apellidoP,
        lastNameMaternal: c.apellidoM,
        birthDate: c.fechaNac,
        gender: i % 2 ? 'male' : 'female',
        isForeign: false,
        mobilePhone: c.telefono,
        // Capture sends the Candidates app invitation (S5): never to the prototype's real-looking addresses.
        email: c.correo ? c.correo.replace(/@.*$/, '@local.test') : null,
        street: c.calle ?? null,
        extNumber: c.numero ?? null,
        neighborhood: c.colonia ?? null,
        postalCode: c.cp ?? null,
        municipality: c.municipio ?? null,
        state: c.estado ?? null,
      },
      jobOpeningId: ids[c.vacIndex],
      source: SOURCE[c.fuente],
      offeredSalary: c.sueldo.toFixed(2),
      comments: null,
      // Captured a few days after the vacante opened, so días de proceso look like the prototype's.
      capturedAt: addDays(openings[c.vacIndex].abierta, 2 + (i % 6)),
    });
    candidateIds.push(body.id);
    const stage = STAGE[c.etapa];
    const hiredAt = addDays(openings[c.vacIndex].abierta, 14 + (i % 6));
    if (stage !== 'new') await api(token, 'POST', `/v1/candidates/${body.id}/stage`, { stage, reason: REASON[stage] ?? null, hiredAt: stage === 'hired' ? hiredAt : null });
  }

  const notes = [
    '<h2>Entrevista telefónica</h2><p>Buena comunicación y disponibilidad inmediata para el turno de día.</p><ul><li><p>Vive cerca de la sucursal</p></li><li><p>Experiencia en atención al cliente</p></li><li><p>Pide pago por Banorte</p></li></ul>',
    '<p><strong>Referencias verificadas.</strong> Perfil en <a href="https://www.linkedin.com/">LinkedIn</a>.</p>',
  ];
  for (const html of notes) await api(token, 'POST', `/v1/candidates/${candidateIds[0]}/notes`, { contentHtml: html, contentJson: null });

  for (const [i, v] of openings.entries()) {
    if (v.estatus === 'Pausada') await api(token, 'POST', `/v1/openings/${ids[i]}/status`, { status: 'paused', reason: 'El cliente pidió esperar' });
    if (v.estatus === 'Cancelada') await api(token, 'POST', `/v1/openings/${ids[i]}/status`, { status: 'cancelled', reason: 'El cliente canceló el proyecto' });
  }
  console.log(`vacantes: ${ids.length} created; candidatos: ${candidateIds.length}`);
}

// ---- S5: a Cliente user for HEB (Clients app, email-link sign-in) ----
const HEB_USER = { email: 'heb@local.test', name: 'Gabriela Villarreal' };
if (emails.has(HEB_USER.email)) {
  console.log(`client user already exists: ${HEB_USER.email}`);
} else {
  const heb = (await api(token, 'GET', '/v1/clients')).body.find((c) => c.shortName === 'HEB');
  await api(token, 'POST', '/v1/users', { email: HEB_USER.email, displayName: HEB_USER.name, role: 'client', clientId: heb.id, grants: null, clientContactId: null });
  console.log(`client user: ${HEB_USER.email} (Clients app, sign-in link from the Auth emulator)`);
}

// ---- S7: payroll groups, puestos, employees ----
if ((await api(token, 'GET', '/v1/payroll/groups')).body.length > 0) {
  console.log('payroll groups: already there, skipped');
} else {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Monterrey' }).format(new Date());
  const clients = Object.fromEntries((await api(token, 'GET', '/v1/clients')).body.map((c) => [c.shortName, c.id]));
  const banks = Object.fromEntries((await api(token, 'GET', '/v1/catalogs')).body.banks.map((b) => [b.speiCode, b.id]));
  const BANK_CODE = { Banorte: '072', Banregio: '058', 'Banco Azteca': '127', BBVA: '012', Santander: '014' };

  // Sites the schedules use (ECOVIA's two stations already come from the S3 seed).
  const siteIds = {};
  for (const [client, name] of [['ECOVIA', 'ECOVIA — Estación Centro'], ['ECOVIA', 'ECOVIA — Estación Norte'], ['HEB', 'HEB — Oficinas Cumbres'], ['CONSTRUCTORA HFC', 'CONSTRUCTORA HFC — Obra Apodaca']]) {
    let site = (await api(token, 'GET', `/v1/clients/${clients[client]}`)).body.sites.find((x) => x.name === name);
    if (!site) site = (await api(token, 'POST', `/v1/clients/${clients[client]}/sites`, { name, zoneId: null })).body.sites.find((x) => x.name === name);
    siteIds[name] = site.id;
  }

  const CUTOFF = { Domingo: 0, Lunes: 1, Martes: 2, Miércoles: 3, Jueves: 4, Viernes: 5, Sábado: 6 };
  const DESCRIPTION = { PG1: 'LUNES A DOMINGO 12 HRS CORTE VIERNES A JUEVES', PG2: 'LUNES A SÁBADO 8 HRS CORTE DOMINGO A SÁBADO', PG3: 'LUNES A VIERNES 8 HRS QUINCENAL' };
  const groupIds = {};
  const positionIds = {};
  for (const g of samplePayrollGroups()) {
    // SEM 38 runs viernes–jueves: the ECOVIA period ends on jueves.
    const cutoff = g.id === 'PG1' ? 4 : CUTOFF[g.corte];
    const { body } = await api(token, 'POST', '/v1/payroll/groups', {
      clientId: clients[g.cliente], name: g.nombre, periodType: g.tipo === 'Quincenal' ? 'biweekly' : 'weekly', cutoffWeekday: cutoff,
      shiftHours: String(g.horas), attendanceLockTime: `${g.bloqueo}:00`, seventhDayDivisor: 6, sundayPremiumPct: '0.25',
      holidayWorkedMultiplier: '2', scheduleDescription: DESCRIPTION[g.id], timeZone: null, isActive: true,
    });
    groupIds[g.id] = body.id;
    for (const p of samplePositions().filter((x) => x.grupoId === g.id)) {
      const after = await api(token, 'POST', `/v1/payroll/groups/${body.id}/positions`, {
        name: p.nombre, dailySalaryPocket: p.sdb.toFixed(2), dailySalaryImss: p.imss.toFixed(2), hourlyRate: p.hora.toFixed(2),
        // SEM 38's hire dates go back to 2023.
        daysWorkedBonus: p.bono.toFixed(2), validFrom: g.id === 'PG1' ? '2020-01-01' : '2025-01-01',
      });
      positionIds[p.id] = after.body.positions.find((x) => x.name === p.nombre).id;
    }
  }

  // Valid numbers (the prototype's are random digits): Luhn for cards, control digit and bank prefix for CLABEs.
  const digitsOf = (seed, len) => { let out = ''; let x = seed * 7919 + 17; for (let k = 0; k < len; k++) { x = (x * 1103515245 + 12345) % 2147483648; out += x % 10; } return out; };
  const luhn = (body) => { for (let d = 0; d < 10; d++) { const n = body + d; let sum = 0; [...n].reverse().forEach((ch, i) => { let v = +ch; if (i % 2) { v *= 2; if (v > 9) v -= 9; } sum += v; }); if (sum % 10 === 0) return n; } };
  const clabe = (bankCode, seed) => { const body = bankCode + '580' + digitsOf(seed, 11); const w = [3, 7, 1]; const sum = [...body].reduce((a, ch, i) => a + (+ch * w[i % 3]) % 10, 0); return body + ((10 - (sum % 10)) % 10); };
  const card = (seed) => luhn('415231' + digitsOf(seed, 9));
  const holder = (name) => name.toUpperCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  const schedule = (employeeId, site, weekdays, start, end, validFrom, job) => api(token, 'POST', `/v1/employees/${employeeId}/schedules`, {
    clientSiteId: siteIds[site], weekdays, oneOffDate: null, startTime: start, endTime: end, validFrom, validTo: null, jobDescription: job,
  });
  // Status periods start after Semana 38, so its golden week stays as the workbook has it.
  const afterSem38 = (date) => (date > SEM38.end ? date : addDays(SEM38.end, 1));
  let created = 0;

  // ECOVIA: the 115 rows of SEM 38.
  const ecoviaPositions = Object.fromEntries(samplePositions().filter((p) => p.grupoId === 'PG1').map((p) => [p.nombre, p]));
  const THIRD_PARTY = { 1: 'JOSE ALFREDO CERVANTES LARA', 4: 'LETICIA MORA SANCHEZ', 6: 'MARIA ELENA TORRES GARCIA' };
  const NO_ACCOUNT = { 12: 'banorte', 16: 'banregio' };
  const sem38 = sem38Rows();
  // One incapacidad (the first row the workbook marks INCAPACIDAD).
  const sickRow = sem38.find((x) => x.comments?.includes('INCAPACIDAD')).row;
  for (const [k, r] of sem38.entries()) {
    const person = (await api(token, 'POST', '/v1/people', {
      firstNames: r.firstNames, lastNamePaternal: r.lastNamePaternal, lastNameMaternal: r.lastNameMaternal, birthDate: null, birthState: null,
      gender: 'unspecified', isForeign: r.foreign, curp: null, rfc: null, nss: null, mobilePhone: null, email: null, street: null,
      extNumber: null, neighborhood: null, postalCode: null, municipality: null, state: null, source: null, notes: null,
    })).body;
    const bankCode = r.bank ?? '127';
    const isCard = bankCode === '127';
    const bankAccount = k in NO_ACCOUNT ? null : {
      bankId: banks[bankCode], accountType: isCard ? 'debit_card' : 'clabe', accountNumber: isCard ? card(k + 100) : clabe(bankCode, k + 100),
      holderName: holder(r.fullName), alias: isCard ? 'Tarjeta' : 'CLABE', validFrom: r.hiredAt, makePrimary: true,
    };
    // A row paid at other rates than its puesto's carries them as overrides (row 94: a validador at supervisor rates).
    const puesto = ecoviaPositions[r.puesto];
    const own = r.rates.pocket !== puesto.sdb || r.rates.hourly !== puesto.hora || r.rates.imss !== puesto.imss;
    const employee = (await api(token, 'POST', '/v1/employees', {
      personId: person.id, payrollGroupId: groupIds.PG1, positionId: positionIds[puesto.id], shift: r.turno, hiredAt: r.hiredAt,
      dailySalaryPocketOverride: own ? r.rates.pocket.toFixed(2) : null, dailySalaryImssOverride: own ? r.rates.imss.toFixed(2) : null,
      hourlyRateOverride: own ? r.rates.hourly.toFixed(2) : null, daysWorkedBonusOverride: null,
      notes: null, cardBank: bankAccount ? null : NO_ACCOUNT[k], candidateId: null, bankAccount,
    })).body;
    if (THIRD_PARTY[k]) {
      await api(token, 'POST', `/v1/employees/${employee.id}/bank-accounts`, {
        bankId: banks['127'], accountType: 'own_account', accountNumber: digitsOf(k + 50, 14), holderName: THIRD_PARTY[k],
        alias: 'Cuenta familiar', validFrom: afterSem38(addDays(today, -30)), makePrimary: k === 6,
      });
    }
    if (k === 18) {
      // An account that is no longer valid: no principal and no request, so Personal offers «Solicitar tarjeta».
      const accounts = (await api(token, 'GET', `/v1/employees/${employee.id}/bank-accounts`)).body;
      await api(token, 'PUT', `/v1/employees/${employee.id}/bank-accounts/${accounts[0].id}`, { holderName: accounts[0].holderName, alias: accounts[0].alias, validTo: today });
    }
    const status = (s, start, end, reason = null, reference = null) =>
      api(token, 'POST', `/v1/employees/${employee.id}/status`, { status: s, startDate: start, endDate: end, reason, reference });
    if (k === 1) await status('vacation', afterSem38(addDays(today, -10)), today);
    if (k === 10) await status('vacation', afterSem38(addDays(today, -2)), addDays(today, 5));
    const baja = r.comments?.includes('BAJA');
    if (baja) await status('terminated', afterSem38(addDays(today, -20)), null, 'Renuncia voluntaria');
    if (r.row === sickRow) {
      await status('sick_leave', afterSem38(addDays(today, -20)), addDays(today, 4), null, 'ST-7 448120');
    }
    // Lunes a domingo with one rest day, 12 h by turno; day shifts at Centro, night shifts at Norte.
    if (!baja) {
      const night = r.turno === 'C/NOCHE';
      await schedule(employee.id, night ? 'ECOVIA — Estación Norte' : 'ECOVIA — Estación Centro', [1, 2, 3, 4, 5, 6, 7].filter((d) => d !== (k % 7) + 1),
        night ? '19:00:00' : '07:00:00', night ? '07:00:00' : '19:00:00', r.hiredAt, null);
    }
    created++;
  }

  // HFC and HEB: the prototype's.
  for (const [i, e] of sampleEmployees().entries()) {
    if (e.cliente === 'ECOVIA') continue;
    // The prototype spaces hire dates 23 days apart from Feb 2025, which runs into 2027: later ones are kept in the past.
    const hiredAt = e.ingreso < today ? e.ingreso : addDays(today, -7 * (i - 20));
    const person = (await api(token, 'POST', '/v1/people', {
      firstNames: e.nombres, lastNamePaternal: e.apellidoP, lastNameMaternal: e.apellidoM, birthDate: e.fechaNac, birthState: null,
      gender: e.genero === 'F' ? 'female' : 'male', isForeign: e.estadoNac === 'Extranjero', curp: null, rfc: null, nss: e.nss,
      mobilePhone: e.celular, email: e.correo.replace(/@.*$/, '@local.test'), street: e.calle, extNumber: e.numero, neighborhood: e.colonia,
      postalCode: e.cp, municipality: e.municipio, state: e.estado, source: null, notes: null,
    })).body;
    const bankName = ['BBVA', 'Banregio', 'Banco Azteca'][i % 3];
    const isClabe = bankName === 'BBVA';
    const employee = (await api(token, 'POST', '/v1/employees', {
      personId: person.id, payrollGroupId: groupIds[e.grupoId], positionId: positionIds[e.puestoId], shift: e.turno, hiredAt,
      dailySalaryPocketOverride: null, dailySalaryImssOverride: null, hourlyRateOverride: null, daysWorkedBonusOverride: null,
      notes: null, cardBank: null, candidateId: null,
      bankAccount: {
        bankId: banks[BANK_CODE[bankName]], accountType: isClabe ? 'clabe' : 'debit_card',
        accountNumber: isClabe ? clabe(BANK_CODE[bankName], i) : card(i), holderName: holder(e.nombre),
        alias: (isClabe ? 'CLABE ' : 'Tarjeta ') + bankName, validFrom: hiredAt, makePrimary: true,
      },
    })).body;
    if (e.cliente === 'HEB') await schedule(employee.id, 'HEB — Oficinas Cumbres', [1, 2, 3, 4, 5], '07:00:00', '15:00:00', hiredAt, 'Limpieza oficinas');
    else await schedule(employee.id, 'CONSTRUCTORA HFC — Obra Apodaca', [1, 2, 3, 4, 5, 6], '08:00:00', '16:00:00', hiredAt, 'Obra Apodaca');
    created++;
  }
  // A one-off job: three técnicos of HFC on next Saturday's maintenance.
  const saturday = addDays(today, (6 - new Date(`${today}T12:00:00`).getDay() + 7) % 7 || 7);
  const hfc = (await api(token, 'GET', `/v1/employees?clientId=${clients['CONSTRUCTORA HFC']}`)).body.slice(0, 3);
  for (const e of hfc) {
    await api(token, 'POST', `/v1/employees/${e.id}/schedules`, {
      clientSiteId: siteIds['CONSTRUCTORA HFC — Obra Apodaca'], weekdays: [], oneOffDate: saturday,
      startTime: '09:00:00', endTime: '13:00:00', validFrom: null, validTo: null, jobDescription: 'Mantenimiento A/C',
    });
  }
  console.log(`payroll: ${Object.keys(groupIds).length} groups, ${Object.keys(positionIds).length} puestos, ${created} employees`);
}

// ---- S8: field supervisor, periods, attendance ----
const SUPERVISOR = { email: 'supervisor@local.test', name: 'Javier Supervisor ECOVIA' };
if ((await api(token, 'GET', '/v1/users')).body.some((u) => u.email === SUPERVISOR.email)) {
  console.log(`supervisor already exists: ${SUPERVISOR.email}`);
} else {
  const ecovia = (await api(token, 'GET', '/v1/clients')).body.find((c) => c.shortName === 'ECOVIA');
  await signUpInEmulator({ email: SUPERVISOR.email, password: LOCAL_OWNER.password, name: SUPERVISOR.name });
  await api(token, 'POST', '/v1/users', {
    email: SUPERVISOR.email, displayName: SUPERVISOR.name, role: 'administrator', clientId: null,
    grants: [{ module: 'payroll_attendance', clientId: ecovia.id }], clientContactId: null,
  });
  console.log(`supervisor: ${SUPERVISOR.email} / ${LOCAL_OWNER.password} (Asistencia, ECOVIA only)`);

  for (const job of ['period-roll', 'status-roll']) await internal(`jobs/${job}`, {});
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Monterrey' }).format(new Date());
  const yesterday = addDays(today, -1);
  let captured = 0;
  let unlocked = 0;
  for (const site of (await api(token, 'GET', `/v1/attendance/sites?date=${today}`)).body.filter((x) => x.clientName === 'ECOVIA')) {
    const day = (await api(token, 'GET', `/v1/attendance/day?siteId=${site.siteId}&date=${today}`)).body;
    // One partial day and one falta so the screen shows more than «Presente».
    const entries = day.entries.filter((e) => !e.statusPeriod).map((e, i) => ({
      employeeId: e.employeeId, status: i === 2 ? 'partial' : i === 5 ? 'absent' : 'present', hoursWorked: i === 2 ? '8' : null,
      overtimeHours: null, note: null,
    }));
    if (entries.length) await api(token, 'PUT', '/v1/attendance/day', { siteId: site.siteId, date: today, entries });
    captured += entries.length;
    const before = (await api(token, 'GET', `/v1/attendance/day?siteId=${site.siteId}&date=${yesterday}`)).body;
    for (const e of before.entries.filter((x) => !x.statusPeriod && !x.record)) {
      await api(token, 'POST', '/v1/attendance/unlock', {
        employeeId: e.employeeId, date: yesterday, siteId: site.siteId, status: 'present', hoursWorked: null, overtimeHours: null,
        note: null, reason: 'Carga inicial',
      });
      unlocked++;
    }
  }
  await internal('jobs/attendance-lock', {});
  console.log(`attendance: ${captured} ECOVIA days captured today, ${unlocked} yesterday through the unlock path`);
}

// ---- S9: Semana 38 as the client's workbook, and the gate ----
{
  const groups = (await api(token, 'GET', '/v1/payroll/groups')).body;
  const ecoviaClient = (await api(token, 'GET', '/v1/clients')).body.find((c) => c.shortName === 'ECOVIA');
  const ecovia = groups.find((g) => g.clientId === ecoviaClient.id);
  const periods = (await api(token, 'GET', `/v1/payroll/periods?groupId=${ecovia.id}`)).body;
  if (periods.some((p) => p.startDate === SEM38.start)) {
    console.log('semana 38: already there, skipped');
  } else {
    // The two bajas of the workbook are terminated after Semana 38: still theirs to pay.
    const employees = Object.fromEntries((await api(token, 'GET', `/v1/employees?clientId=${ecoviaClient.id}&includeTerminated=true`)).body
      .map((e) => [e.fullName, e.id]));
    const rows = sem38Rows();
    const writes = rows.flatMap((r) => sem38Week(r).map((d) => ({ employeeId: employees[r.fullName], ...d })));
    if (writes.some((w) => !w.employeeId)) throw new Error('SEM 38: an employee of the workbook is missing from ECOVIA');
    // Every September day is locked: written as changes with a reason, eight at a time.
    let next = 0;
    const refused = [];
    await Promise.all(Array.from({ length: 8 }, async () => {
      while (next < writes.length) {
        const w = writes[next++];
        const { status, body } = await api(token, 'POST', '/v1/attendance/unlock', {
          employeeId: w.employeeId, date: w.date, siteId: null, status: w.status, hoursWorked: w.hoursWorked, overtimeHours: w.overtimeHours,
          note: null, reason: 'Carga inicial SEM 38',
        });
        if (status !== 200) refused.push(`${w.date} ${w.status}: ${body.detail ?? status}`);
      }
    }));
    // api() lets a 409 through; here every refused day would break the golden week.
    if (refused.length) throw new Error(`SEM 38: ${refused.length} days refused, e.g. ${refused.slice(0, 5).join('; ')}`);
    // Semana 38 and the week the S8 seed captured yesterday in (42); the lock job closes both to Revisión.
    const created38 = (await api(token, 'POST', `/v1/payroll/groups/${ecovia.id}/periods`, { date: SEM38.start })).body;
    const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Monterrey' }).format(new Date());
    await api(token, 'POST', `/v1/payroll/groups/${ecovia.id}/periods`, { date: addDays(today, -1) });
    await internal('jobs/attendance-lock', {});

    let grid = (await api(token, 'GET', `/v1/payroll/periods/${created38.id}`)).body;
    const lines = Object.fromEntries(grid.rows.map((r) => [r.employeeId, r.lineId]));
    for (const r of rows.filter((x) => x.comments)) {
      await api(token, 'PUT', `/v1/payroll/lines/${lines[employees[r.fullName]]}/comments`, { comments: r.comments });
    }
    grid = (await api(token, 'GET', `/v1/payroll/periods/${created38.id}`)).body;
    const got = { totalNet: grid.totalNet, totalNetExact: grid.totalNetExact, rows: grid.rows.length };
    const same = +got.totalNet === +GOLDEN.totalNet && +got.totalNetExact === +GOLDEN.totalNetExact && got.rows === GOLDEN.rows;
    if (!same) throw new Error(`SEM 38 gate failed: expected ${JSON.stringify(GOLDEN)}, the grid has ${JSON.stringify(got)}`);
    console.log(`semana 38: ${writes.length} days loaded, ${grid.rows.length} lines in ${grid.period.status}; total ${got.totalNet} (exact ${got.totalNetExact}) = golden`);
  }
}
