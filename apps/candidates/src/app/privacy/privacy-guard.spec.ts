import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CandidatePortal } from '../data/portal';
import { routes } from '../app.routes';
import { privacyAcceptedGuard } from './privacy-guard';

const run = (acceptedAt: string | null) => {
  TestBed.configureTestingModule({
    providers: [
      { provide: CandidatePortal, useValue: { getProfile: async () => ({ privacyNoticeAcceptedAt: acceptedAt }) } },
      { provide: Router, useValue: { createUrlTree: (commands: string[]) => ({ redirect: commands.join('') }) } },
    ],
  });
  return TestBed.runInInjectionContext(() => privacyAcceptedGuard({} as never, {} as never));
};

describe('privacyAcceptedGuard', () => {
  it('lets a candidate who accepted the aviso de privacidad in', async () => {
    expect(await run('2026-10-01T10:00:00Z')).toBe(true);
  });

  it('sends one who has not accepted it to the aviso', async () => {
    TestBed.resetTestingModule();
    expect(await run(null)).toEqual({ redirect: '/aviso' });
  });

  it('is what gates every screen of the candidates shell', () => {
    const shell = routes.find((r) => r.path === '')!;
    expect(shell.canActivateChild).toContain(privacyAcceptedGuard);
  });
});
