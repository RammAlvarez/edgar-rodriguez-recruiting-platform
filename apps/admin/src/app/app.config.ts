import {
  type ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsMx from '@angular/common/locales/es-MX';
import { provideRouter, Router, RouteReuseStrategy, withComponentInputBinding } from '@angular/router';
import type { ApiError, RuntimeConfig } from '@platform/api-client';
import { API_CLIENT, provideApiClient } from '@platform/ui/api';
import { providePlatformMaterial } from '@platform/ui/material';
import { Session } from '@platform/ui/session';
import type { PasswordAuthProvider } from '../auth/firebase-auth';
import { PASSWORD_AUTH } from './auth-token';
import { routes } from './app.routes';
import { ParamAwareRouteReuseStrategy } from './route-reuse';
import { CoverMode } from './shell/cover-mode';

registerLocaleData(localeEsMx);

export function createAppConfig(config: RuntimeConfig, auth: PasswordAuthProvider): ApplicationConfig {
  const coverMode = new CoverMode();
  // The API client is built before the router exists: the initializer below fills this in.
  let onMfaRequired: (error: ApiError) => void = () => undefined;
  // Every call of a burst (a screen loading several lists) is refused at once: only the first one moves the app.
  let routing = false;
  return {
    providers: [
      providePlatformMaterial(),
      provideBrowserGlobalErrorListeners(),
      provideZonelessChangeDetection(),
      provideRouter(routes, withComponentInputBinding()),
      { provide: RouteReuseStrategy, useClass: ParamAwareRouteReuseStrategy },
      { provide: LOCALE_ID, useValue: 'es-MX' },
      { provide: PASSWORD_AUTH, useValue: auth },
      { provide: CoverMode, useValue: coverMode },
      provideApiClient(auth, {
        baseUrl: config.apiUrl,
        actingFor: () => coverMode.recruiterId(),
        onMfaRequired: (error) => onMfaRequired(error),
      }),
      provideAppInitializer(() => {
        const session = inject(Session);
        coverMode.bindUser(() => session.me()?.id ?? null);
        const router = inject(Router);
        const api = inject(API_CLIENT);
        onMfaRequired = (error) => {
          if (routing || router.url.startsWith('/verificacion')) return;
          routing = true;
          // The session's copy of «me» predates the change (the API refuses everything else now): reload it, then show why.
          api.cache.invalidate(['me']);
          void router.navigateByUrl('/verificacion', { state: { notice: error.message } }).finally(() => (routing = false));
        };
      }),
    ],
  };
}
