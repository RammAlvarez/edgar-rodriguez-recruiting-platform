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

  async function start(afterChange: Record<string, unknown>) {
    const user: AuthUser = { uid: 'u-1' } as AuthUser;
    const auth: AuthProvider = { onChange: (l) => (l(user), () => undefined), getToken: async () => 'tok', signOut: async () => undefined };
    const changed = { value: false };
    const meReads = { count: 0 };
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname;
      if (path === '/v1/me/sessions') return json(200, me({}));
      if (path === '/v1/me') {
        meReads.count++;
        return json(200, me(changed.value ? afterChange : {}));
      }
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
    return { router, fixture, changed, meReads };
  }

  it('sends the user to the setup with the API message instead of failing silently', async () => {
    const { router, fixture, changed } = await start({ mfaRequired: true });

    changed.value = true;
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

  it('asks for the code when it is set up but not passed yet', async () => {
    const { router, fixture, changed } = await start({ mfaRequired: true, mfaEnabled: true, mfaVerified: false });

    changed.value = true;
    await expect(TestBed.inject(API_CLIENT).get('/v1/users')).rejects.toMatchObject({ isMfaRequired: true });
    await vi.waitFor(() => expect(router.url).toBe('/verificacion'));
    await vi.waitFor(() => {
      fixture.detectChanges();
      const page = fixture.nativeElement as HTMLElement;
      expect(page.querySelector('[role="alert"]')?.textContent).toContain(NOTICE);
      expect(page.textContent).toContain('Escribe el código de 6 dígitos de tu app');
      expect(page.textContent).not.toContain('Generar código QR');
    });
  });

  it('reloads «me» and navigates once for a burst of refused calls', async () => {
    const { router, changed, meReads } = await start({ mfaRequired: true });
    const navigate = vi.spyOn(router, 'navigateByUrl');
    const before = meReads.count;

    changed.value = true;
    const api = TestBed.inject(API_CLIENT);
    await Promise.all(Array.from({ length: 5 }, () => api.get('/v1/users').catch(() => undefined)));
    await vi.waitFor(() => expect(router.url).toBe('/verificacion'));
    await TestBed.inject(ApplicationRef).whenStable();

    expect(navigate).toHaveBeenCalledTimes(1);
    expect(meReads.count - before).toBe(1);
  });
});
