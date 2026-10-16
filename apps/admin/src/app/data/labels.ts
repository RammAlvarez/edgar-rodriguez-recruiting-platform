import type { PermissionModule, UserRole } from '@platform/shared';

export const ROLE_LABELS: Record<UserRole, string> = {
  administrator: 'Administrador',
  recruiter: 'Reclutador',
  client: 'Cliente',
};

/** Delegated grants in the order of the prototype's «Permisos delegados» (SPEC §2.2). */
export const GRANTS: readonly { readonly module: PermissionModule; readonly label: string; readonly allClientsOnly?: boolean }[] = [
  { module: 'recruiting_reports', label: 'Reportes de reclutamiento', allClientsOnly: true },
  { module: 'payroll_prepayroll', label: 'Prenómina' },
  { module: 'payroll_attendance', label: 'Asistencia' },
  { module: 'payroll_incidents', label: 'Incidencias' },
  { module: 'payroll_staff', label: 'Personal' },
  { module: 'payroll_payment_files', label: 'Archivos de pago' },
  { module: 'payroll_card_requests', label: 'Solicitud de tarjetas' },
  { module: 'users', label: 'Usuarios', allClientsOnly: true },
];

/** A new Administrador starts with every grant except Reportes (SPEC §2.2, D5) — the server applies the same default. */
export const DEFAULT_ADMIN_GRANTS: readonly PermissionModule[] = GRANTS.map((g) => g.module).filter((m) => m !== 'recruiting_reports');
