import type { Provider } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';

/** Design-system defaults for Angular Material: outlined form fields (`appearance="outline"`, RASS readme "Forms"). */
export function providePlatformMaterial(): Provider[] {
  return [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } }];
}
