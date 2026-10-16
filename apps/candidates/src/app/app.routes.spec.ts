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
  it('has the email-link sign-in, the aviso de privacidad and a candidate-only shell behind it', () => {
    const paths = routes.map((r) => r.path);
    expect(paths).toEqual(expect.arrayContaining(['entrar', 'entrar/enlace', 'aviso', 'sin-acceso']));
    const shell = routes.find((r) => r.path === '');
    expect(shell?.canActivate?.length).toBe(2);
    expect(shell?.canActivateChild?.length).toBe(1);
    expect(shell?.children?.map((c) => c.path)).toEqual(['', 'perfil', 'documentos', 'postulacion']);
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
});
