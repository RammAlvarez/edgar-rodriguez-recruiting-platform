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
      'recruiting_reports', 'payroll_prepayroll', 'payroll_attendance', 'payroll_incidents',
      'payroll_staff', 'payroll_payment_files', 'payroll_card_requests', 'users',
    ]);
    expect(paths(owner)).toEqual([
      'inicio', 'vacantes', 'candidatos', 'reclutadores', 'clientes', 'reportes', 'personal', 'calendario',
      'asistencia', 'incidencias', 'prenomina', 'clientes-nomina', 'reportes-nomina', 'usuarios',
    ]);
  });

  it('hides Reportes from an administrator without the grant (SPEC D5)', () => {
    expect(paths(me('administrator', ['users']))).not.toContain('reportes');
  });

  // PLAN P5: SPEC D19 says a supervisor "sees nothing else", but permission_module has no recruiting grant, so the
  // matrix as written still shows Reclutamiento. This test pins the current matrix until P5 is decided.
  it('shows a supervisor with only Asistencia the attendance screens, plus recruiting (no grant exists yet, P5)', () => {
    const supervisor = me('administrator', ['payroll_attendance']);
    expect(paths(supervisor)).toEqual(['inicio', 'vacantes', 'candidatos', 'reclutadores', 'clientes', 'calendario', 'asistencia']);
    expect(homePath(supervisor)).toBe('inicio');
  });

  it('shows a recruiter only their workspace', () => {
    const recruiter = me('recruiter');
    expect(paths(recruiter)).toEqual(['mis-vacantes', 'mis-candidatos']);
    expect(homePath(recruiter)).toBe('mis-vacantes');
  });
});
