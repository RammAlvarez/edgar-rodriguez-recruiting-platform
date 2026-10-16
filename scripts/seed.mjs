#!/usr/bin/env node
// npm run seed — local sample data, created THROUGH THE API so derived data is computed by the real code.
// Refuses a non-local database. Grows with each slice of docs/PLAN.md; today (S0): the local owner account,
// signed up in the Auth emulator with a known password and bootstrapped as the first Administrador.
import { assertLocalDatabase, loadEnv } from './lib/env.mjs';

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

await signUpInEmulator(LOCAL_OWNER);
const result = await internal('bootstrap', { email: LOCAL_OWNER.email, displayName: LOCAL_OWNER.name });
if (result.status === 200) console.log(`owner created: ${LOCAL_OWNER.email} / ${LOCAL_OWNER.password}`);
else if (result.status === 409) console.log(`owner already exists: ${LOCAL_OWNER.email} / ${LOCAL_OWNER.password}`);
else throw new Error(`bootstrap failed (${result.status}): ${JSON.stringify(result.body)}`);
