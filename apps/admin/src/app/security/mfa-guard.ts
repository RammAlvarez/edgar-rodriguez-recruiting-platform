import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import type { MeDto } from '@platform/shared';
import { Session } from '@platform/ui/session';

/** Staff who have the verificación en dos pasos, or must, and have not passed it in this sign-in (PLAN D89). */
export function mfaPending(me: MeDto | null): boolean {
  if (!me || (me.role !== 'administrator' && me.role !== 'recruiter')) return false;
  return (me.mfaEnabled || me.mfaRequired) && !me.mfaVerified;
}

/** Every screen of the shell waits for the code (the API refuses the rest anyway). */
export const mfaGuard: CanActivateFn = () => (mfaPending(inject(Session).me()) ? inject(Router).createUrlTree(['/verificacion']) : true);
