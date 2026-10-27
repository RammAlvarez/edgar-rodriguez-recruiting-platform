import { ApplicationRef, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, RouterOutlet } from '@angular/router';
import type { AuthProvider, AuthUser, RuntimeConfig } from '@platform/api-client';
import { API_CLIENT } from '@platform/ui/api';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createAppConfig } from '../app.config';

@Component({ selector: 'app-host', imports: [RouterOutlet], template: '<router-outlet />' })
class Host {}

const NOTICE = 'Tu cuenta requiere verificación en dos pasos: actívala para continuar.';
const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const me = (over: Record<string, unknown>) => ({
  id: 'u-1', displayName: 'Ana Admin', email: 'ana@x.mx', role: 'administrator', clientId: null, mfaRequired: false, mfaEnabled: false,
  mfaVerified: false, grants: [], actingFor: null, personId: null, ...over,
});

// Found on the GCP dev deploy (2026-10-27, HANDOFF §11): requiring MFA for your own account left every screen failing, silently.
describe('when two-step verification becomes required', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sends the user to the setup with the API message instead of failing silently', async () => {
    const user: AuthUser = { uid: 'u-1' } as AuthUser;
    const auth: AuthProvider = { onChange: (l) => (l(user), () => undefined), getToken: async () => 'tok', signOut: async () => undefined };
    const requiredNow = { value: false };
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname;
      if (path === '/v1/me/sessions') return json(200, me({}));
      if (path === '/v1/me') return json(200, me({ mfaRequired: requiredNow.value }));
      return json(403, { type: 'https://platform.local/problems/mfa', title: 'MFA', detail: NOTICE });
    });
    vi.stubGlobal('fetch', fetchMock);

    const config = { apiUrl: 'http://api.test', auth: {} } as unknown as RuntimeConfig;
    TestBed.configureTestingModule(createAppConfig(config, { ...auth, signInWithPassword: async () => undefined, sendPasswordReset: async () => undefined }));
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(Host);
    router.initialNavigation();
    await TestBed.inject(ApplicationRef).whenStable();
    expect(router.url).not.toBe('/verificacion');

    requiredNow.value = true;
    await expect(TestBed.inject(API_CLIENT).get('/v1/users')).rejects.toMatchObject({ isMfaRequired: true });
    await vi.waitFor(() => expect(router.url).toBe('/verificacion'));
    await vi.waitFor(() => {
      fixture.detectChanges();
      const page = fixture.nativeElement as HTMLElement;
      expect(page.querySelector('h1')?.textContent).toContain('Verificación en dos pasos');
      expect(page.querySelector('[role="alert"]')?.textContent).toContain(NOTICE);
      expect(page.textContent).toContain('Tu cuenta requiere este paso extra');
      expect(page.textContent).toContain('Generar código QR');
    });
  });
});
