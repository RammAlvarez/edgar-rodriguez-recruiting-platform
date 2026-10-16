import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './api-client';
import type { AuthProvider } from './auth';
import { ApiError } from './errors';
import { isoDateReviver } from './json';

const auth = (token: string | null = 'tok'): AuthProvider => ({
  onChange: () => () => undefined,
  getToken: async () => token,
  signOut: async () => undefined,
});

const respond = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  });

describe('ApiClient', () => {
  it('sends the bearer token and the cover-mode header', async () => {
    const fetch = vi.fn(async () => respond(200, { ok: true }));
    const client = new ApiClient({ baseUrl: 'http://api.test', auth: auth(), actingFor: () => 'rec-1', fetch });
    await client.get('/v1/me');
    const [url, init] = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('http://api.test/v1/me');
    const headers = new Headers(init.headers);
    expect(headers.get('Authorization')).toBe('Bearer tok');
    expect(headers.get('X-Acting-For')).toBe('rec-1');
  });

  it('revives timestamps but keeps business dates and decimals as strings', async () => {
    const fetch = vi.fn(async () =>
      respond(200, { at: '2026-10-16T07:43:57.123Z', day: '2026-09-17', amount: '1234.56' }),
    );
    const client = new ApiClient({ baseUrl: 'http://api.test', auth: auth(), fetch });
    const body = await client.get<{ at: Date; day: string; amount: string }>('/x');
    expect(body.at).toBeInstanceOf(Date);
    expect(body.day).toBe('2026-09-17');
    expect(body.amount).toBe('1234.56');
  });

  it('maps 400 to field errors and 403 to the server message', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(respond(400, { title: 'Revisa los campos marcados', errors: { email: ['Introduce un correo válido.'] } }))
      .mockResolvedValueOnce(respond(403, { detail: 'No tienes permiso de «Usuarios».' }));
    const client = new ApiClient({ baseUrl: 'http://api.test', auth: auth(), fetch });

    const invalid = await client.post('/v1/users', {}).catch((e: unknown) => e);
    expect(invalid).toBeInstanceOf(ApiError);
    expect((invalid as ApiError).fieldError('email')).toBe('Introduce un correo válido.');

    const denied = await client.get('/v1/users').catch((e: unknown) => e);
    expect((denied as ApiError).status).toBe(403);
    expect((denied as ApiError).message).toBe('No tienes permiso de «Usuarios».');
  });

  it('re-fetches tagged queries from X-Invalidates, on errors too', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(respond(204, undefined, { 'X-Invalidates': 'users,me' }))
      .mockResolvedValueOnce(respond(409, { detail: 'Conflicto' }, { 'X-Invalidates': 'periods' }));
    const client = new ApiClient({ baseUrl: 'http://api.test', auth: auth(), fetch });
    const users = vi.fn();
    const periods = vi.fn();
    const other = vi.fn();
    client.cache.register(['users'], users);
    client.cache.register(['periods'], periods);
    client.cache.register(['clients'], other);

    await client.post('/v1/users', { email: 'a@b.mx' });
    await client.post('/v1/periods/close').catch(() => undefined);

    expect(users).toHaveBeenCalledTimes(1);
    expect(periods).toHaveBeenCalledTimes(1);
    expect(other).not.toHaveBeenCalled();
  });

  it('turns a network failure into a Spanish ApiError', async () => {
    const client = new ApiClient({
      baseUrl: 'http://api.test',
      auth: auth(null),
      fetch: vi.fn(async () => {
        throw new TypeError('Failed to fetch');
      }),
    });
    await expect(client.get('/v1/me')).rejects.toMatchObject({ status: 0, message: expect.stringContaining('Sin conexión') });
  });
});

describe('cache', () => {
  it('refreshAll re-fetches every registered query and unregister stops it', () => {
    const client = new ApiClient({ baseUrl: 'http://api.test', auth: auth() });
    const a = vi.fn();
    const unregister = client.cache.register(['a'], a);
    client.cache.refreshAll();
    unregister();
    client.cache.refreshAll();
    expect(a).toHaveBeenCalledTimes(1);
  });
});

describe('isoDateReviver', () => {
  it.each(['2026-10-16T07:43:57Z', '2026-10-16T01:43:57-06:00'])('revives %s', (v) =>
    expect(isoDateReviver('', v)).toBeInstanceOf(Date));
  it.each(['2026-10-16', '2026-10-16T07:43', 'hola', '12.50'])('leaves %s alone', (v) => expect(isoDateReviver('', v)).toBe(v));
});
