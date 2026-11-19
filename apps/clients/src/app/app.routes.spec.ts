import { TestBed } from '@angular/core/testing';
import type { CanActivateFn } from '@angular/router';
import { guardHarness, makeMe } from '@platform/ui/testing/guard-harness';
import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { countOf, FUNNEL, STAGE, type ClientOpeningDto } from './data/portal';

describe('routes', () => {
  const shell = () => routes.find((r) => r.path === '')!;
  const route = (path: string) => routes.find((r) => r.path === path)!;

  it('lets only a Cliente into the shell: others go to «sin acceso», visitors to the sign-in', async () => {
    const client = guardHarness({ status: 'ready', me: makeMe('client') });
    expect(await client.runAll(shell().canActivate as CanActivateFn[])).toBe(true);
    TestBed.resetTestingModule();
    const admin = guardHarness({ status: 'ready', me: makeMe('administrator') });
    expect(await admin.runAll(shell().canActivate as CanActivateFn[])).toEqual({ redirect: '/sin-acceso' });
    TestBed.resetTestingModule();
    const visitor = guardHarness({ status: 'signed-out' });
    expect(await visitor.runAll(shell().canActivate as CanActivateFn[])).toEqual({ redirect: '/entrar' });
  });

  it('shows the sign-in only to visitors; the email link and «sin acceso» are open to anyone', async () => {
    const signedIn = guardHarness({ status: 'ready', me: makeMe('client') });
    expect(await signedIn.runAll(route('entrar').canActivate as CanActivateFn[])).toEqual({ redirect: '/' });
    expect(route('entrar/enlace').canActivate).toBeUndefined();
    expect(route('sin-acceso').canActivate).toBeUndefined();
  });

  it('opens every item of the top navigation, plus the detail of a vacante', () => {
    const items = (shell().data?.['items'] as { path: string }[]).map((i) => i.path);
    const children = shell().children!.map((c) => c.path);
    for (const item of items) expect(children).toContain(item);
    expect(children).toContain('vacantes/:id');
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
