import { InjectionToken, type Provider } from '@angular/core';
import { ApiClient, type ApiClientOptions, type AuthProvider, type EmailLinkAuthProvider } from '@platform/api-client';

/** The app's single {@link ApiClient}; repositories inject it, components never call fetch. */
export const API_CLIENT = new InjectionToken<ApiClient>('API_CLIENT');
export const AUTH_PROVIDER = new InjectionToken<AuthProvider>('AUTH_PROVIDER');

export function provideApiClient(auth: AuthProvider, options: Omit<ApiClientOptions, 'auth'>): Provider[] {
  return [
    { provide: AUTH_PROVIDER, useValue: auth },
    { provide: API_CLIENT, useValue: new ApiClient({ ...options, auth }) },
  ];
}

/** The candidates/clients apps' passwordless provider (same object as AUTH_PROVIDER). */
export const EMAIL_LINK_AUTH = new InjectionToken<EmailLinkAuthProvider>('EMAIL_LINK_AUTH');
