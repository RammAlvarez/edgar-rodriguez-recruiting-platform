import { registerLocaleData } from '@angular/common';
import localeEsMx from '@angular/common/locales/es-MX';
import {
  type ApplicationConfig,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import type { EmailLinkAuthProvider, RuntimeConfig } from '@platform/api-client';
import { EMAIL_LINK_AUTH, provideApiClient } from '@platform/ui/api';
import { providePlatformMaterial } from '@platform/ui/material';
import { routes } from './app.routes';

registerLocaleData(localeEsMx);

export function createAppConfig(config: RuntimeConfig, auth: EmailLinkAuthProvider): ApplicationConfig {
  return {
    providers: [
      providePlatformMaterial(),
      provideBrowserGlobalErrorListeners(),
      provideZonelessChangeDetection(),
      provideRouter(routes, withComponentInputBinding()),
      { provide: LOCALE_ID, useValue: 'es-MX' },
      { provide: EMAIL_LINK_AUTH, useValue: auth },
      provideApiClient(auth, { baseUrl: config.apiUrl }),
    ],
  };
}
