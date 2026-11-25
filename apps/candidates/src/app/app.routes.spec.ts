import { TestBed } from '@angular/core/testing';
import type { CanActivateFn } from '@angular/router';
import { guardHarness, makeMe } from '@platform/ui/testing/guard-harness';
import { describe, expect, it } from 'vitest';
import { statusMessage, STEPS } from './application/application-page';
import { routes } from './app.routes';
import type { CandidateApplicationDto } from './data/portal';

const app = (over: Partial<CandidateApplicationDto>): CandidateApplicationDto => ({
  id: 'a',
  openingName: 'HEB – CAJERO',
  step: 'received',
  outcome: 'in_process',
  capturedAt: '2026-10-01' as CandidateApplicationDto['capturedAt'],
  hiredAt: null,
  recruiterName: null,
  recruiterPhone: null,
  recruiterEmail: null,
  nextInterviewAt: null,
  nextInterviewLocation: null,
  ...over,
});

describe('routes', () => {
  const shell = () => routes.find((r) => r.path === '')!;
  const route = (path: string) => routes.find((r) => r.path === path)!;

  it('lets only a candidate into the shell: others go to «sin acceso», visitors to the sign-in', async () => {
    const candidate = guardHarness({ status: 'ready', me: makeMe('candidate') });
    expect(await candidate.runAll(shell().canActivate as CanActivateFn[])).toBe(true);
    TestBed.resetTestingModule();
    const client = guardHarness({ status: 'ready', me: makeMe('client') });
    expect(await client.runAll(shell().canActivate as CanActivateFn[])).toEqual({ redirect: '/sin-acceso' });
    TestBed.resetTestingModule();
    const visitor = guardHarness({ status: 'signed-out' });
    expect(await visitor.runAll(shell().canActivate as CanActivateFn[])).toEqual({ redirect: '/entrar' });
  });

  it('shows the sign-in only to visitors; the email link and «sin acceso» are open to anyone', async () => {
    const signedIn = guardHarness({ status: 'ready', me: makeMe('candidate') });
    expect(await signedIn.runAll(route('entrar').canActivate as CanActivateFn[])).toEqual({ redirect: '/' });
    expect(route('entrar/enlace').canActivate).toBeUndefined();
    expect(route('sin-acceso').canActivate).toBeUndefined();
  });

  it('guards the aviso de privacidad like the shell (it needs a candidate session)', async () => {
    const client = guardHarness({ status: 'ready', me: makeMe('client') });
    expect(await client.runAll(route('aviso').canActivate as CanActivateFn[])).toEqual({ redirect: '/sin-acceso' });
  });

  it('opens every item of the top navigation', () => {
    const items = (shell().data?.['items'] as { path: string }[]).map((i) => i.path);
    const children = shell().children!.map((c) => c.path);
    for (const item of items) expect(children).toContain(item);
  });
});

describe('Mi postulación', () => {
  it('tracks four friendly steps', () => {
    expect(STEPS.map((s) => s.label)).toEqual(['Recibida', 'Contactado', 'Entrevista', 'Resultado']);
  });

  it('never tells the candidate why a process closed', () => {
    expect(statusMessage(app({ step: 'result', outcome: 'closed' }))).toBe('Este proceso ya concluyó. Gracias por tu interés.');
    expect(statusMessage(app({ step: 'interview' }))).toBe('Estás en la etapa de entrevista.');
    expect(statusMessage(app({ step: 'result', outcome: 'selected' }))).toContain('presentarte con la empresa');
  });

  it('tells a hire that the company hired them, never that the platform pays them (D101)', () => {
    const hired = statusMessage(app({ step: 'result', outcome: 'hired' }));
    expect(hired).toContain('La empresa te contrató');
    expect(hired).not.toMatch(/pag|nómina/i);
  });
});
