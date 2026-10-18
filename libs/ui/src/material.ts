import { type EnvironmentProviders, inject, type Provider, provideAppInitializer } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_SELECT_CONFIG } from '@angular/material/select';
import { ThemeService } from './theme';

/** Design-system defaults for Angular Material: outlined form fields (`appearance="outline"`, RASS readme "Forms"), the saved theme. */
export function providePlatformMaterial(): (Provider | EnvironmentProviders)[] {
  return [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
    // A «Todos» / «Sin …» option with a null value is shown as the selection, as in the prototype's filters.
    { provide: MAT_SELECT_CONFIG, useValue: { canSelectNullableOptions: true } }, provideAppInitializer(() => void inject(ThemeService))];
}
