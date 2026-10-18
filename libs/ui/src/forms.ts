import { signal } from '@angular/core';
import type { AbstractControl, FormGroup, ValidatorFn } from '@angular/forms';
import { ApiError } from '@platform/api-client';
import type { z } from 'zod';

/**
 * Client-side checks give the server's message earlier; the server's 400 is authoritative and lands on the same
 * controls (`server` error), cleared as soon as the user edits the form.
 */
export function zodValidator(schema: z.ZodType): ValidatorFn {
  return (control) => {
    const value: unknown = control.value;
    if (value === null || value === undefined || value === '') return null;
    const result = schema.safeParse(value);
    return result.success ? null : { message: result.error.issues[0]?.message ?? 'Valor inválido.' };
  };
}

/**
 * Puts each 400 field message on its control; returns the messages that matched no control (shown above the actions).
 * The marks clear on the next edit anywhere in the form, not only in the marked field: a rule can name one field while the
 * fix is in another («Hasta 30 prestaciones» lands on Prestaciones, the user shortens Otras prestaciones), and a mark that
 * stayed would keep the form invalid and block every later Guardar. The server checks again on the next submit.
 */
export function applyServerErrors(form: FormGroup, error: ApiError): string[] {
  const unmatched: string[] = [];
  const marked: AbstractControl[] = [];
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const control = form.get(field);
    const message = messages[0];
    if (!message) continue;
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: message });
      control.markAsTouched();
      marked.push(control);
    } else {
      unmatched.push(message);
    }
  }
  if (marked.length) {
    const sub = form.valueChanges.subscribe(() => {
      sub.unsubscribe();
      for (const control of marked) {
        if (!control.errors?.['server']) continue;
        const rest = { ...control.errors };
        delete rest['server'];
        control.setErrors(Object.keys(rest).length ? rest : null);
      }
    });
  }
  return unmatched;
}

/** The message `mat-error` shows for a control. */
export function errorMessage(control: AbstractControl | null): string | null {
  const e = control?.errors;
  if (!e) return null;
  if (typeof e['server'] === 'string') return e['server'];
  if (typeof e['message'] === 'string') return e['message'];
  if (e['required']) return 'Este campo es obligatorio.';
  if (e['email']) return 'Introduce un correo válido.';
  if (e['maxlength']) return `Admite hasta ${(e['maxlength'] as { requiredLength: number }).requiredLength} caracteres.`;
  if (e['min']) return `El valor mínimo es ${(e['min'] as { min: number }).min}.`;
  return 'Revisa este campo.';
}

/** Empty strings become null before a request (the API treats both the same, the DTOs say nullable). */
export function blankToNull<T extends Record<string, unknown>>(value: T): { [K in keyof T]: T[K] extends string ? T[K] | null : T[K] } {
  return Object.fromEntries(
    Object.entries(value).map(([k, v]) => [k, typeof v === 'string' && v.trim() === '' ? null : v]),
  ) as never;
}

/** Shared submit plumbing of forms and side panels: busy flag, 400 → fields, anything else → one alert line. */
export class PanelState {
  readonly busy = signal(false);
  readonly alert = signal<string | null>(null);

  constructor(private readonly form: FormGroup) {}

  msg(field: string): string | null {
    return errorMessage(this.form.get(field));
  }

  async run(work: () => Promise<void>): Promise<void> {
    // A second click or Enter before the button shows as disabled must not send the form twice (D-024).
    if (this.busy()) return;
    // A server alert from the last attempt no longer applies once the user tries again.
    this.alert.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.busy.set(true);
    this.alert.set(null);
    try {
      await work();
    } catch (e) {
      if (e instanceof ApiError && e.isValidation) {
        const marked = Object.keys(e.fieldErrors).length > 0;
        // A 400 without field errors (an unreadable value) still says what happened.
        this.alert.set(applyServerErrors(this.form, e)[0] ?? (marked ? null : e.message));
      } else this.alert.set(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.');
    } finally {
      this.busy.set(false);
    }
  }
}
