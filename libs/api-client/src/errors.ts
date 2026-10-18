/**
 * A failed API call: 400 carries field issues (camelCase path → messages); 401/403/404/409/429 carry one user-facing message,
 * and 429 (PLAN D92) the seconds of its `Retry-After`.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Readonly<Record<string, readonly string[]>> = {},
    readonly retryAfterSeconds: number | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isValidation(): boolean {
    return this.status === 400;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isRateLimited(): boolean {
    return this.status === 429;
  }

  /** First message for a form field, for `mat-error`. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

const FALLBACK = 'No se pudo completar la operación. Inténtalo de nuevo.';
const RATE_LIMITED = 'Hiciste demasiadas solicitudes seguidas. Espera un momento e inténtalo de nuevo.';
const NETWORK = 'Sin conexión con el servidor. Revisa tu internet e inténtalo de nuevo.';

export async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    // not a problem-details body
  }
  const problem = (body ?? {}) as { detail?: string; title?: string; errors?: Record<string, string[]> };
  // A 400's message is its first field error when it has one: a toast (a stage move, a dialog that already closed) then
  // says what to fix instead of «Revisa los campos marcados» (D-026). Forms still put each error on its field.
  const firstField = Object.values(problem.errors ?? {}).flat()[0];
  const fallback =
    response.status === 400 ? (firstField ?? problem.title ?? 'Revisa los campos marcados') : response.status === 429 ? RATE_LIMITED : FALLBACK;
  const retryAfter = response.status === 429 ? Number.parseInt(response.headers.get('Retry-After') ?? '', 10) : Number.NaN;
  return new ApiError(response.status, problem.detail ?? fallback, problem.errors ?? {}, Number.isNaN(retryAfter) ? null : retryAfter);
}

export const networkError = (): ApiError => new ApiError(0, NETWORK);
