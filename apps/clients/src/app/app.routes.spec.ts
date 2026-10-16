import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { countOf, FUNNEL, STAGE, type ClientOpeningDto } from './data/portal';

describe('routes', () => {
  it('has the email-link sign-in and a Cliente-only shell with the vacantes, their detail and the comments', () => {
    const paths = routes.map((r) => r.path);
    expect(paths).toEqual(expect.arrayContaining(['entrar', 'entrar/enlace', 'sin-acceso']));
    const shell = routes.find((r) => r.path === '');
    expect(shell?.canActivate?.length).toBe(2);
    expect(shell?.children?.map((c) => c.path)).toEqual(['', 'vacantes', 'vacantes/:id', 'comentarios']);
  });
});

describe('portal labels', () => {
  it('reads Entrevista as «En entrevista» and funnels the five forward stages', () => {
    expect(STAGE.interview.label).toBe('En entrevista');
    expect(FUNNEL.map((f) => f.label)).toEqual(['Nuevo', 'Contactado', 'Entrevista', 'Apto', 'Contratado']);
  });

  it('counts a stage that has no candidates as zero', () => {
    const o = { stageCounts: [{ stage: 'new', count: 2 }] } as unknown as ClientOpeningDto;
    expect(countOf(o, 'new')).toBe(2);
    expect(countOf(o, 'hired')).toBe(0);
  });
});
