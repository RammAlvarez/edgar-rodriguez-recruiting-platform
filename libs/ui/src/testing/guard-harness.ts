import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type CanActivateFn, Router } from '@angular/router';
import type { MeDto, MeRole, PermissionModule } from '@platform/shared';
import { firstValueFrom, type Observable } from 'rxjs';
import { hasGrant, Session, type SessionState } from '../session';

/** For specs only (not exported from the index): the signed-in user as `/v1/me` would describe them. */
export const makeMe = (role: MeRole, grants: PermissionModule[] = []): MeDto =>
  ({ id: 'u', displayName: 'U', email: 'u@x', role, clientId: null, mfaRequired: false, mfaEnabled: false, mfaVerified: false,
    grants: grants.map((module) => ({ module, clientId: null })), actingFor: null, personId: null }) as unknown as MeDto;

/**
 * Runs the real route guards against a stub Session and Router, as the router would: a redirect comes back as
 * `{ redirect: '/path' }`. Call once per test (it configures the TestBed) and flip `state` to simulate `/v1/me` settling.
 */
export function guardHarness(initial: SessionState) {
  const state = signal<SessionState>(initial);
  const me = computed(() => {
    const s = state();
    return s.status === 'ready' ? s.me : null;
  });
  const session = {
    state: state.asReadonly(),
    me,
    // The same function Session.hasGrant delegates to.
    hasGrant: (module: PermissionModule) => hasGrant(me(), module),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: Session, useValue: session },
      { provide: Router, useValue: { createUrlTree: (commands: string[]) => ({ redirect: commands.join('') }) } },
    ],
  });

  const run = async (guard: CanActivateFn): Promise<unknown> => {
    const result = TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as Observable<unknown> | unknown;
    const settled = firstValueFrom(result as Observable<unknown>);
    TestBed.tick();
    return settled;
  };

  /** All guards of a route in order; the first one that does not answer `true` decides, like the router. */
  const runAll = async (guards: readonly CanActivateFn[] | undefined): Promise<unknown> => {
    for (const guard of guards ?? []) {
      const answer = await run(guard);
      if (answer !== true) return answer;
    }
    return true;
  };

  return { state, run, runAll };
}
