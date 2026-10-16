import { DestroyRef, inject, signal, type Signal } from '@angular/core';
import { ApiError } from '@platform/api-client';
import { API_CLIENT } from './api';

export interface LiveQuery<T> {
  readonly value: Signal<T | undefined>;
  readonly error: Signal<ApiError | undefined>;
  readonly loading: Signal<boolean>;
  reload(): void;
}

/**
 * A query that re-runs whenever a mutation invalidates one of its tags (X-Invalidates) or the user presses
 * «Actualizar». Call it in an injection context (a repository method used from a component field initializer).
 */
export function liveQuery<T>(load: () => Promise<T>, tags: readonly string[]): LiveQuery<T> {
  const api = inject(API_CLIENT);
  const value = signal<T | undefined>(undefined);
  const error = signal<ApiError | undefined>(undefined);
  const loading = signal(false);
  let run = 0;

  const reload = () => {
    const current = ++run;
    loading.set(true);
    load().then(
      (result) => {
        if (current !== run) return;
        value.set(result);
        error.set(undefined);
        loading.set(false);
      },
      (e: unknown) => {
        if (current !== run) return;
        error.set(e instanceof ApiError ? e : new ApiError(0, 'Ocurrió un error inesperado.'));
        loading.set(false);
      },
    );
  };

  const unregister = api.cache.register(tags, reload);
  inject(DestroyRef).onDestroy(unregister);
  reload();
  return { value: value.asReadonly(), error: error.asReadonly(), loading: loading.asReadonly(), reload };
}
