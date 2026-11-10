import { TestBed } from '@angular/core/testing';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui';
import { guardHarness, makeMe } from '@platform/ui/testing/guard-harness';
import { describe, expect, it } from 'vitest';

// libs/ui has no Angular test runner of its own, so the shared route guards are exercised through the apps' runners.

describe('signedInGuard', () => {
  it('lets a signed-in user with a users row through', async () => {
    expect(await guardHarness({ status: 'ready', me: makeMe('administrator') }).run(signedInGuard)).toBe(true);
  });

  it('sends a signed-out visitor to the sign-in page and an account without access to «sin acceso»', async () => {
    expect(await guardHarness({ status: 'signed-out' }).run(signedInGuard)).toEqual({ redirect: '/entrar' });
    TestBed.resetTestingModule();
    expect(await guardHarness({ status: 'denied', message: 'x' }).run(signedInGuard)).toEqual({ redirect: '/sin-acceso' });
  });

  it('waits while /v1/me is loading instead of deciding on a stale state', async () => {
    const { state, run } = guardHarness({ status: 'loading' });
    let answered = false;
    const pending = run(signedInGuard).then((r) => ((answered = true), r));
    await Promise.resolve();
    expect(answered).toBe(false);
    state.set({ status: 'ready', me: makeMe('administrator') });
    TestBed.tick();
    expect(await pending).toBe(true);
  });
});

describe('signedOutGuard', () => {
  it('is only for signed-out visitors', async () => {
    expect(await guardHarness({ status: 'signed-out' }).run(signedOutGuard)).toBe(true);
    TestBed.resetTestingModule();
    expect(await guardHarness({ status: 'ready', me: makeMe('administrator') }).run(signedOutGuard)).toEqual({ redirect: '/' });
  });
});

describe('accessGuard', () => {
  it('sends a role outside the app to «sin acceso» (never to "/", which would loop)', async () => {
    const { run } = guardHarness({ status: 'ready', me: makeMe('client') });
    expect(await run(accessGuard(['administrator', 'recruiter']))).toEqual({ redirect: '/sin-acceso' });
  });

  it('needs the grant for an administrator, and sends one without it home', async () => {
    const withGrant = guardHarness({ status: 'ready', me: makeMe('administrator', ['payroll_staff']) });
    expect(await withGrant.run(accessGuard(['administrator'], 'payroll_staff'))).toBe(true);
    TestBed.resetTestingModule();
    const without = guardHarness({ status: 'ready', me: makeMe('administrator', ['recruiting']) });
    expect(await without.run(accessGuard(['administrator'], 'payroll_staff'))).toEqual({ redirect: '/' });
  });

  it('does not ask a non-administrator for a grant, nor an administrator when no grant is named', async () => {
    const recruiter = guardHarness({ status: 'ready', me: makeMe('recruiter') });
    expect(await recruiter.run(accessGuard(['recruiter', 'administrator'], 'payroll_staff'))).toBe(true);
    TestBed.resetTestingModule();
    const admin = guardHarness({ status: 'ready', me: makeMe('administrator') });
    expect(await admin.run(accessGuard(['administrator']))).toBe(true);
  });

  it('sends someone without a users row to «sin acceso»', async () => {
    const { run } = guardHarness({ status: 'denied', message: 'x' });
    expect(await run(accessGuard(['administrator']))).toEqual({ redirect: '/sin-acceso' });
  });
});
