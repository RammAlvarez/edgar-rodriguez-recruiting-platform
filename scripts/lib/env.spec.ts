import { describe, expect, it } from 'vitest';
// @ts-expect-error plain .mjs without types
import { assertConfirmedIfRemote, assertLocalDatabase, assertLocalDatabases } from './env.mjs';

const local = 'postgres://platform_api:x@localhost:5433/platform';
const remote = 'postgres://platform_api:x@10.20.0.5:5432/platform';

describe('assertLocalDatabase', () => {
  it('accepts databases on this machine', () => {
    expect(() => assertLocalDatabase(local)).not.toThrow();
    expect(() => assertLocalDatabase('Host=127.0.0.1;Database=platform')).not.toThrow();
  });

  it('reads a URL without userinfo or with a bracketed IPv6 host', () => {
    expect(() => assertLocalDatabase('postgres://localhost/platform')).not.toThrow();
    expect(() => assertLocalDatabase('postgres://u:p@[::1]:5433/platform')).not.toThrow();
    expect(() => assertLocalDatabase('Server=localhost;Port=5433;Database=platform')).not.toThrow();
  });

  it('is not fooled by a ?host= override that points elsewhere', () => {
    expect(() => assertLocalDatabase('postgres://u:p@localhost/platform?host=10.20.0.5')).toThrow(/non-local/);
    expect(() => assertLocalDatabase('postgres://u:p@localhost/platform?host=localhost')).not.toThrow();
  });

  it('refuses remote, socket-only or unparseable targets', () => {
    expect(() => assertLocalDatabase(remote)).toThrow(/non-local/);
    expect(() => assertLocalDatabase('Host=/cloudsql/proj:region:db;Database=platform')).toThrow(/non-local/);
    expect(() => assertLocalDatabase('')).toThrow(/non-local/);
  });
});

describe('assertLocalDatabases', () => {
  it('checks both the API and the migrations URL', () => {
    expect(() => assertLocalDatabases({ DATABASE_URL: local, MIGRATIONS_DATABASE_URL: local })).not.toThrow();
    expect(() => assertLocalDatabases({ DATABASE_URL: local, MIGRATIONS_DATABASE_URL: remote })).toThrow(/MIGRATIONS_DATABASE_URL/);
    expect(() => assertLocalDatabases({ DATABASE_URL: remote })).toThrow(/DATABASE_URL/);
  });

  it('also checks the URLs the API tests truncate through', () => {
    expect(() => assertLocalDatabases({ TEST_MIGRATIONS_DATABASE_URL: remote })).toThrow(/TEST_MIGRATIONS_DATABASE_URL/);
    expect(() => assertLocalDatabases({ TEST_DATABASE_URL: remote })).toThrow(/TEST_DATABASE_URL/);
  });

  it('is skipped only with the explicit opt-out', () => {
    expect(() => assertLocalDatabases({ DATABASE_URL: remote, PLATFORM_ALLOW_REMOTE_DB: '1' })).not.toThrow();
    expect(() => assertLocalDatabases({ DATABASE_URL: remote, PLATFORM_ALLOW_REMOTE_DB: 'true' })).toThrow();
  });
});

describe('assertConfirmedIfRemote', () => {
  it('lets a local API through without --yes', () => {
    expect(() => assertConfirmedIfRemote('http://localhost:5080', false, 'Bootstrap')).not.toThrow();
    expect(() => assertConfirmedIfRemote('http://127.0.0.1:5080', false, 'Bootstrap')).not.toThrow();
  });

  it('requires --yes for a deployed API', () => {
    expect(() => assertConfirmedIfRemote('https://api.example.com', false, 'Bootstrap')).toThrow(/--yes/);
    expect(() => assertConfirmedIfRemote('https://api.example.com', true, 'Bootstrap')).not.toThrow();
  });

  it('rejects an invalid URL', () => {
    expect(() => assertConfirmedIfRemote('not a url', true, 'Bootstrap')).toThrow(/valid URL/);
  });
});
