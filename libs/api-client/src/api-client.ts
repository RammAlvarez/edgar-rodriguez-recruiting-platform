import type { AuthProvider } from './auth';
import { INVALIDATES_HEADER, parseInvalidates, QueryCache } from './cache';
import { ApiError, networkError, toApiError } from './errors';
import { parseJson } from './json';

export interface ApiClientOptions {
  /** e.g. "https://api.example.com" or "http://localhost:5080". */
  readonly baseUrl: string;
  readonly auth: AuthProvider;
  readonly cache?: QueryCache;
  /** Recruiter uid while an Administrador is in "Ver como reclutador" (sent as X-Acting-For). */
  readonly actingFor?: () => string | null;
  /** Injected for tests; defaults to the global fetch. */
  readonly fetch?: typeof fetch;
}

type Query = Record<string, string | number | boolean | null | undefined>;

/**
 * The only place the apps call `fetch` (components and repositories go through this). Adds the bearer token,
 * revives timestamps, maps failures to {@link ApiError} and applies `X-Invalidates` — on errors too.
 */
export class ApiClient {
  readonly cache: QueryCache;
  private readonly fetchImpl: typeof fetch;

  constructor(private readonly options: ApiClientOptions) {
    this.cache = options.cache ?? new QueryCache();
    this.fetchImpl = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  }

  get<T>(path: string, query?: Query): Promise<T> {
    return this.request<T>('GET', path, undefined, query);
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  patch<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PATCH', path, body);
  }

  delete<T = void>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }

  private async request<T>(method: string, path: string, body?: unknown, query?: Query): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' });
    const token = await this.options.auth.getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const actingFor = this.options.actingFor?.();
    if (actingFor) headers.set('X-Acting-For', actingFor);
    if (body !== undefined) headers.set('Content-Type', 'application/json');

    let response: Response;
    try {
      response = await this.fetchImpl(this.url(path, query), {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw networkError();
    }

    this.cache.invalidate(parseInvalidates(response.headers.get(INVALIDATES_HEADER)));
    if (!response.ok) throw await toApiError(response);
    if (response.status === 204) return undefined as T;
    return parseJson<T>(await response.text());
  }

  private url(path: string, query?: Query): string {
    const url = new URL(path.replace(/^\//, ''), this.options.baseUrl.replace(/\/?$/, '/'));
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined && value !== '') url.searchParams.set(key, String(value));
    }
    return url.toString();
  }
}

export { ApiError };
