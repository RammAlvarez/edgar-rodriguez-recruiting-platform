import type { MeDto, PermissionModule, UserRole } from '@platform/shared';
import { describe, expect, it } from 'vitest';
import { homePath, visibleNavigation } from './navigation';

const me = (role: UserRole, grants: PermissionModule[] = []): MeDto => ({
  id: '00000000-0000-0000-0000-000000000001',
  displayName: 'Prueba',
  email: 'p@example.test',
  role,
  clientId: null,
  mfaRequired: false,
  grants: grants.map((module) => ({ module, clientId: null })),
  actingFor: null,
});

const paths = (m: MeDto) => visibleNavigation(m).flatMap((s) => s.items.map((i) => i.path));

describe('admin navigation', () => {
  it('shows the owner every section, including both report screens', () => {
    const owner = me('administrator', [
      'recruiting', 'recruiting_reports', 'payroll_prepayroll', 'payroll_attendance', 'payroll_incidents',
      'payroll_staff', 'payroll_payment_files', 'payroll_card_requests', 'users',
    ]);
    expect(paths(owner)).toEqual([
      'inicio', 'vacantes', 'candidatos', 'reclutadores', 'clientes', 'reportes', 'personal', 'calendario',
      'asistencia', 'incidencias', 'prenomina', 'clientes-nomina', 'reportes-nomina', 'usuarios', 'catalogos',
    ]);
  });

  it('hides Reportes from an administrator without the grant (SPEC D5)', () => {
    expect(paths(me('administrator', ['users']))).not.toContain('reportes');
  });

  // PLAN P5 (a) / D69: SPEC D19's field supervisor is an Administrador with only Asistencia and sees nothing else.
  it('shows a supervisor with only Asistencia the attendance screens and nothing of recruiting', () => {
    const supervisor = me('administrator', ['payroll_attendance']);
    expect(paths(supervisor)).toEqual(['calendario', 'asistencia']);
    expect(homePath(supervisor)).toBe('calendario');
  });

  it('shows Reclutamiento, Inicio and Catálogos only with the recruiting grant', () => {
    expect(paths(me('administrator', ['recruiting']))).toEqual(['inicio', 'vacantes', 'candidatos', 'reclutadores', 'clientes', 'catalogos']);
  });

  it('shows a recruiter only their workspace', () => {
    const recruiter = me('recruiter');
    expect(paths(recruiter)).toEqual(['mis-vacantes', 'mis-candidatos']);
    expect(homePath(recruiter)).toBe('mis-vacantes');
  });

  it('has no home for a Cliente user, who is sent to "sin acceso" instead of looping', () => {
    expect(paths(me('client'))).toEqual([]);
    expect(homePath(me('client'))).toBeNull();
  });
});
