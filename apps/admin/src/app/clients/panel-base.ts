import { signal } from '@angular/core';
import type { FormGroup } from '@angular/forms';
import { ApiError } from '@platform/api-client';
import { applyServerErrors, errorMessage } from '@platform/ui';

/** Shared submit plumbing of the side panels: busy flag, 400 → fields, anything else → one alert line. */
export class PanelState {
  readonly busy = signal(false);
  readonly alert = signal<string | null>(null);

  constructor(private readonly form: FormGroup) {}

  msg(field: string): string | null {
    return errorMessage(this.form.get(field));
  }

  async run(work: () => Promise<void>): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.busy.set(true);
    this.alert.set(null);
    try {
      await work();
    } catch (e) {
      if (e instanceof ApiError && e.isValidation) this.alert.set(applyServerErrors(this.form, e)[0] ?? null);
      else this.alert.set(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.');
    } finally {
      this.busy.set(false);
    }
  }
}
