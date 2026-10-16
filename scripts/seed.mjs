#!/usr/bin/env node
// npm run seed — local sample data, created THROUGH THE API so derived data is computed by the real code.
// Refuses a non-local database. Grows with each slice of docs/PLAN.md, and is safe to re-run:
//   S0  the local owner, signed up in the Auth emulator with a known password and bootstrapped as the first Administrador;
//   S3  this year's and next year's LFT holidays, the recruiting puestos, the 11 clients of design-reference/data.js
//       (razón social, RFC, contacto principal, one site with its zona) and the 6 recruiters (same local password);
//   S4  the 13 vacantes and 30 candidatos of data.js at their etapas (HEB has two open CAJERO vacantes, so the date suffix
//       shows), two hires that fill GTS, the paused / cancelled vacantes, and the two sample notes. Skipped once any
//       vacante exists.
import { assertLocalDatabase, loadEnv } from './lib/env.mjs';
import { addDays, candidatos as sampleCandidates, clients as sampleClients, RECRUITERS, vacantes as sampleOpenings } from '../design-reference/data.js';

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
        email: c.correo,
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
