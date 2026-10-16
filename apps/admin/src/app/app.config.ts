import {
  type ApplicationConfig,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsMx from '@angular/common/locales/es-MX';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import type { RuntimeConfig } from '@platform/api-client';
import { provideApiClient } from '@platform/ui/api';
import { providePlatformMaterial } from '@platform/ui/material';
import type { PasswordAuthProvider } from '../auth/firebase-auth';
import { PASSWORD_AUTH } from './auth-token';
import { routes } from './app.routes';
import { CoverMode } from './shell/cover-mode';

registerLocaleData(localeEsMx);

export function createAppConfig(config: RuntimeConfig, auth: PasswordAuthProvider): ApplicationConfig {
  const coverMode = new CoverMode();
  return {
    providers: [
      providePlatformMaterial(),
      provideBrowserGlobalErrorListeners(),
      provideZonelessChangeDetection(),
      provideRouter(routes, withComponentInputBinding()),
      { provide: LOCALE_ID, useValue: 'es-MX' },
      { provide: PASSWORD_AUTH, useValue: auth },
      { provide: CoverMode, useValue: coverMode },
      provideApiClient(auth, { baseUrl: config.apiUrl, actingFor: () => coverMode.recruiterId() }),
    ],
  };
}
