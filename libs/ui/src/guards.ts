import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { type CanActivateFn, Router } from '@angular/router';
import type { MeRole, PermissionModule } from '@platform/shared';
import { filter, map, take } from 'rxjs';
import { Session, type SessionState } from './session';

const settled = () =>
  toObservable(inject(Session).state).pipe(
    filter((s): s is Exclude<SessionState, { status: 'loading' }> => s.status !== 'loading'),
    take(1),
  );

/** Signed in with a users row: continue; signed out → sign-in page; no access → the "sin acceso" page. */
export const signedInGuard: CanActivateFn = () => {
  const router = inject(Router);
  return settled().pipe(
    map((s) =>
      s.status === 'ready' ? true : router.createUrlTree([s.status === 'denied' ? '/sin-acceso' : '/entrar']),
    ),
  );
};

/** The sign-in page is only for signed-out visitors. */
export const signedOutGuard: CanActivateFn = () => {
  const router = inject(Router);
  return settled().pipe(map((s) => (s.status === 'signed-out' ? true : router.createUrlTree(['/']))));
};

/** UI-side mirror of a permission-matrix row; the API enforces the same rule. */
export const accessGuard =
  (roles: readonly MeRole[], grant?: PermissionModule): CanActivateFn =>
  () => {
    const session = inject(Session);
    const router = inject(Router);
    // Waits for /v1/me like signedInGuard: guards of one route run together, so this one may start while it is loading.
    return settled().pipe(
      map(() => {
        const me = session.me();
        // A role outside the app (e.g. a Cliente in the admin app) gets the "sin acceso" page; redirecting to "/" would loop.
        if (!me || !roles.includes(me.role)) return router.createUrlTree(['/sin-acceso']);
        const allowed = !grant || me.role !== 'administrator' || session.hasGrant(grant);
        return allowed || router.createUrlTree(['/']);
      }),
    );
  };
