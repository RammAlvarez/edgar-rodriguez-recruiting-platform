#!/usr/bin/env node
// Production start: creates the first Administrador (the owner, with every grant) through the API and prints the
// link to set the password. Refuses once an administrator exists. Run by a human:
//   API_URL=https://api.example.com INTERNAL_SECRET=… node scripts/bootstrap.mjs --email dueno@empresa.mx --name "Edgar Rodríguez" --yes
// `--yes` is required when API_URL is not on this machine.
import { parseArgs } from 'node:util';
import { assertConfirmedIfRemote, loadEnv } from './lib/env.mjs';

const { values } = parseArgs({ options: { email: { type: 'string' }, name: { type: 'string' }, yes: { type: 'boolean', default: false } } });
if (!values.email || !values.name) {
  console.error('usage: node scripts/bootstrap.mjs --email <email> --name "<display name>" [--yes]');
  process.exit(2);
}
const env = loadEnv();
try {
  assertConfirmedIfRemote(env.API_URL, values.yes, 'Bootstrap');
} catch (error) {
  console.error(error.message);
  process.exit(2);
}
const response = await fetch(`${env.API_URL.replace(/\/$/, '')}/internal/bootstrap`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-Internal-Secret': env.INTERNAL_SECRET },
  body: JSON.stringify({ email: values.email, displayName: values.name }),
});
const body = await response.json().catch(() => ({}));
if (!response.ok) {
  console.error(`Bootstrap failed (${response.status}): ${body.detail ?? JSON.stringify(body.errors ?? body)}`);
  process.exit(1);
}
console.log(`Administrador creado: ${body.email} (${body.id})`);
console.log(`Enlace para crear la contraseña:\n${body.passwordSetupLink}`);
