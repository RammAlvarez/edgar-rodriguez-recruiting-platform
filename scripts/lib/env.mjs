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

/** The seed and other local-only scripts refuse anything that is not a database on this machine. */
export function assertLocalDatabase(url = process.env.DATABASE_URL ?? '') {
  const host = /^postgres(?:ql)?:\/\/[^@]*@([^:/?]+)/.exec(url)?.[1] ?? /Host=([^;]+)/i.exec(url)?.[1];
  if (!host || !['localhost', '127.0.0.1', '::1', 'postgres'].includes(host)) {
    throw new Error(`Refusing to run against a non-local database (host "${host ?? '?'}").`);
  }
}
