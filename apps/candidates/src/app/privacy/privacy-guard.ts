import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { CandidatePortal } from '../data/portal';

/** The candidate must accept the aviso de privacidad before using the app (ARCHITECTURE §6, PLAN D77). */
export const privacyAcceptedGuard: CanActivateFn = async () => {
  // inject() only works before the first await.
  const portal = inject(CandidatePortal);
  const router = inject(Router);
  const profile = await portal.getProfile();
  return profile.privacyNoticeAcceptedAt ? true : router.createUrlTree(['/aviso']);
};
