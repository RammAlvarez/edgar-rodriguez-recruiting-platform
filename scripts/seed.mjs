#!/usr/bin/env node
// npm run seed — local sample data, created THROUGH THE API so derived data is computed by the real code.
// Refuses a non-local database. Grows with each slice of docs/PLAN.md, and is safe to re-run:
//   S0  the local owner, signed up in the Auth emulator with a known password and bootstrapped as the first Administrador;
//   S3  this year's and next year's LFT holidays, the recruiting puestos, the 11 clients of design-reference/data.js
//       (razón social, RFC, contacto principal, one site with its zona) and the 6 recruiters (same local password).
import { assertLocalDatabase, loadEnv } from './lib/env.mjs';
import { clients as sampleClients, RECRUITERS, vacantes as sampleOpenings } from '../design-reference/data.js';

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
