// Loads the repo-root .env (if any) without overriding variables already set, and derives the local defaults
// every script and `npm run api` use. Production never reads .env: the container gets real env vars.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function loadEnv() {
  const file = resolve(root, '.env');
  if (existsSync(file)) {
    for (const line of readFileSync(file, 'utf8').split(/\r?\n/)) {
      const match = /^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/.exec(line);
      if (match && process.env[match[1]] === undefined) process.env[match[1]] = match[2].replace(/^["']|["']$/g, '');
    }
  }
  const dbPort = process.env.PLATFORM_DB_PORT ?? '5433';
  const s3Port = process.env.PLATFORM_S3_PORT ?? '9000';
  const defaults = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: 'http://localhost:5080',
    DATABASE_URL: `postgres://platform_api:platform_api@localhost:${dbPort}/platform`,
    MIGRATIONS_DATABASE_URL: `postgres://platform_owner:platform_owner@localhost:${dbPort}/platform`,
    INTERNAL_SECRET: 'local-internal-secret-change-me-0123456789',
    API_URL: 'http://localhost:5080',
    FIREBASE_PROJECT_ID: 'demo-platform',
    FIREBASE_AUTH_EMULATOR_HOST: '127.0.0.1:9199',
    S3_ENDPOINT: `http://localhost:${s3Port}`,
    S3_REGION: 'us-east-1',
    S3_ACCESS_KEY_ID: 'minioadmin',
    S3_SECRET_ACCESS_KEY: 'minioadmin',
    S3_BUCKET_DOCUMENTS: 'documents',
    S3_BUCKET_EXPORTS: 'exports',
    S3_FORCE_PATH_STYLE: 'true',
    APP_ORIGINS: 'http://localhost:4300,http://localhost:4301,http://localhost:4302',
  };
  for (const [key, value] of Object.entries(defaults)) process.env[key] ??= value;
  return process.env;
}

const LOCAL_DB_HOSTS = ['localhost', '127.0.0.1', '::1', 'postgres'];
const LOCAL_API_HOSTS = ['localhost', '127.0.0.1', '[::1]', '::1'];

/**
 * The host the API would really connect to for a Postgres URL (`postgres://user@host:port/db?…`) or an Npgsql string
 * (`Host=…;Database=…`); null when there is none or it is a failover list. Mirrors `PostgresConnection.ToConnectionString`, which
 * copies every query key into the Npgsql builder: keys are case-insensitive, `Server` is a synonym of `Host` and the last one wins.
 */
function databaseHost(url) {
  let host = null;
  if (/^postgres(?:ql)?:\/\//i.test(url)) {
    try {
      const parsed = new URL(url);
      host = parsed.hostname;
      for (const [key, value] of parsed.searchParams) if (/^(host|server)$/i.test(key)) host = value;
    } catch {
      return null;
    }
  } else {
    for (const match of url.matchAll(/(?:^|;)\s*(?:Host|Server)\s*=\s*([^;]*)/gi)) host = match[1].trim();
  }
  if (host === null) return null;
  host = host.replace(/^\[|\]$/g, '').toLowerCase();
  return host && !host.includes(',') ? host : null;
}

/** The seed and other local-only scripts refuse anything that is not a database on this machine. */
export function assertLocalDatabase(url = process.env.DATABASE_URL ?? '') {
  const host = databaseHost(url);
  if (!host || !LOCAL_DB_HOSTS.includes(host)) {
    throw new Error(`Refusing to run against a non-local database (host "${host ?? '?'}").`);
  }
}

/**
 * `scripts/run.mjs` (api, db:migrate, test:api) runs against local databases only. A DATABASE_URL or MIGRATIONS_DATABASE_URL
 * exported in the shell or `.env` that points elsewhere (DEV, production) stops here; PLATFORM_ALLOW_REMOTE_DB=1 is the explicit opt-out.
 */
export function assertLocalDatabases(env = process.env) {
  if (env.PLATFORM_ALLOW_REMOTE_DB === '1') return;
  // TEST_* are what the API tests connect to (and truncate, as the owner role).
  for (const name of ['DATABASE_URL', 'MIGRATIONS_DATABASE_URL', 'TEST_DATABASE_URL', 'TEST_MIGRATIONS_DATABASE_URL']) {
    if (!env[name]) continue;
    try {
      assertLocalDatabase(env[name]);
    } catch (error) {
      throw new Error(`${name}: ${error.message} Set PLATFORM_ALLOW_REMOTE_DB=1 only if you really mean to.`, { cause: error });
    }
  }
}

/** Scripts that call a deployed API (bootstrap, import-excel) need `--yes` when API_URL is not on this machine. */
export function assertConfirmedIfRemote(apiUrl, confirmed, action) {
  let host;
  try {
    host = new URL(apiUrl).hostname;
  } catch (error) {
    throw new Error(`API_URL is not a valid URL: "${apiUrl}".`, { cause: error });
  }
  if (!LOCAL_API_HOSTS.includes(host) && !confirmed) {
    throw new Error(`${action} would run against ${apiUrl}, which is not on this machine. Pass --yes to confirm.`);
  }
}
