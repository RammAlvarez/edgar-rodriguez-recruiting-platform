/** A failed API call: 400 carries field issues (camelCase path → messages); 401/403/404/409 carry one user-facing message. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Readonly<Record<string, readonly string[]>> = {},
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

  /** First message for a form field, for `mat-error`. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

const FALLBACK = 'No se pudo completar la operación. Inténtalo de nuevo.';
const NETWORK = 'Sin conexión con el servidor. Revisa tu internet e inténtalo de nuevo.';

export async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    // not a problem-details body
  }
  const problem = (body ?? {}) as { detail?: string; title?: string; errors?: Record<string, string[]> };
  const message = problem.detail ?? (response.status === 400 ? (problem.title ?? 'Revisa los campos marcados') : FALLBACK);
  return new ApiError(response.status, message, problem.errors ?? {});
}

export const networkError = (): ApiError => new ApiError(0, NETWORK);
