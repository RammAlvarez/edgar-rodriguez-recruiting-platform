import { describe, expect, it } from 'vitest';
import type { UserDto } from '../data/api-types';
import { accessSummary, clientColumn, fromGrants, toGrants } from './grants';

describe('grant checkboxes ↔ API grants', () => {
  it('puts the client scope on payroll grants only, never on Usuarios or Reportes', () => {
    expect(toGrants(['payroll_prepayroll', 'users', 'recruiting_reports'], 'c1')).toEqual([
      { module: 'payroll_prepayroll', clientId: 'c1' },
      { module: 'users', clientId: null },
      { module: 'recruiting_reports', clientId: null },
    ]);
  });

  it('reads back one scope, or flags mixed scopes', () => {
    expect(fromGrants(toGrants(['payroll_staff', 'users'], 'c1'))).toEqual({ modules: ['payroll_staff', 'users'], onlyClientId: 'c1', mixed: false });
    expect(
      fromGrants([
        { module: 'payroll_staff', clientId: 'c1' },
        { module: 'payroll_incidents', clientId: null },
      ]).mixed,
    ).toBe(true);
  });
});

// E2E run: an Administrador limited to one client read «—» in the list, like one with every client.
describe('who can reach which clients', () => {
  const names: Record<string, string> = { c1: 'E2E LIMPIEZA', c2: 'ECOVIA' };
  const name = (id: string) => names[id] ?? '—';
  const user = (role: UserDto['role'], grants: UserDto['grants'], clientName: string | null = null) => ({ role, grants, clientName }) as UserDto;

  it('names the client an Administrador is limited to, and Todos when there is no limit', () => {
    expect(clientColumn(user('administrator', toGrants(['payroll_attendance'], 'c1')), name)).toBe('E2E LIMPIEZA');
    expect(clientColumn(user('administrator', toGrants(['payroll_attendance', 'users'], null)), name)).toBe('Todos');
    expect(clientColumn(user('administrator', toGrants(['users'], 'c1')), name)).toBe('Todos');
    expect(clientColumn(user('administrator', [{ module: 'payroll_staff', clientId: 'c1' }, { module: 'payroll_incidents', clientId: null }]), name)).toBe('Según permiso');
    expect(clientColumn(user('client', [], 'HEB'), name)).toBe('HEB');
    expect(clientColumn(user('recruiter', []), name)).toBe('—');
  });

  it('summarises each permission with its clients', () => {
    expect(accessSummary(user('administrator', toGrants(['payroll_attendance'], 'c1')), name)).toBe('Asistencia — solo E2E LIMPIEZA');
    expect(
      accessSummary(user('administrator', [{ module: 'payroll_staff', clientId: 'c1' }, { module: 'payroll_incidents', clientId: null }, { module: 'users', clientId: null }]), name),
    ).toBe('Personal — solo E2E LIMPIEZA · Incidencias, Usuarios — todos los clientes');
    expect(accessSummary(user('client', [], 'HEB'), name)).toBe('Portal de clientes — solo HEB');
    expect(accessSummary(user('administrator', []), name)).toBe('Sin permisos delegados');
  });
});
