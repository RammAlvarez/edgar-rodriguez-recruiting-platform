import type { AbstractControl, FormGroup, ValidatorFn } from '@angular/forms';
import type { ApiError } from '@platform/api-client';
import type { z } from 'zod';

/**
 * Client-side checks give the server's message earlier; the server's 400 is authoritative and lands on the same
 * controls (`server` error), cleared as soon as the user edits the field.
 */
export function zodValidator(schema: z.ZodType): ValidatorFn {
  return (control) => {
    const value: unknown = control.value;
    if (value === null || value === undefined || value === '') return null;
    const result = schema.safeParse(value);
    return result.success ? null : { message: result.error.issues[0]?.message ?? 'Valor inválido.' };
  };
}

/** Puts each 400 field message on its control; returns the messages that matched no control (shown above the actions). */
export function applyServerErrors(form: FormGroup, error: ApiError): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const control = form.get(field);
    const message = messages[0];
    if (!message) continue;
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: message });
      control.markAsTouched();
      const sub = control.valueChanges.subscribe(() => {
        if (control.errors?.['server']) {
          const rest = { ...control.errors };
          delete rest['server'];
          control.setErrors(Object.keys(rest).length ? rest : null);
        }
        sub.unsubscribe();
      });
    } else {
      unmatched.push(message);
    }
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
