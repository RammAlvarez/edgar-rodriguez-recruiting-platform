import type { MeDto, PermissionModule, UserRole } from '@platform/shared';

/** Sidebar of the prototype (design-reference/Plataforma.dc.html), filtered by role and delegated grants. */
export interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: string;
  readonly roles: readonly UserRole[];
  /** Administradores need this grant; other roles ignore it. */
  readonly grant?: PermissionModule;
}

export interface NavSection {
  readonly label: string | null;
  readonly items: readonly NavItem[];
}

const ADMIN: readonly UserRole[] = ['administrator'];
const RECRUITER: readonly UserRole[] = ['recruiter'];

export const NAVIGATION: readonly NavSection[] = [
  { label: null, items: [{ path: 'inicio', label: 'Inicio', icon: 'dashboard', roles: ADMIN }] },
  {
    label: null,
    items: [
      { path: 'mis-vacantes', label: 'Mis vacantes', icon: 'work', roles: RECRUITER },
      { path: 'mis-candidatos', label: 'Mis candidatos', icon: 'group', roles: RECRUITER },
    ],
  },
  {
    label: 'Reclutamiento',
    items: [
      { path: 'vacantes', label: 'Vacantes', icon: 'work', roles: ADMIN },
      { path: 'candidatos', label: 'Candidatos', icon: 'group', roles: ADMIN },
      { path: 'reclutadores', label: 'Reclutadores', icon: 'support_agent', roles: ADMIN },
      { path: 'clientes', label: 'Clientes', icon: 'domain', roles: ADMIN },
      { path: 'reportes', label: 'Reportes', icon: 'bar_chart', roles: ADMIN, grant: 'recruiting_reports' },
    ],
  },
  {
    label: 'Nómina',
    items: [
      { path: 'personal', label: 'Personal', icon: 'badge', roles: ADMIN, grant: 'payroll_staff' },
      { path: 'calendario', label: 'Calendario', icon: 'calendar_month', roles: ADMIN, grant: 'payroll_attendance' },
      { path: 'asistencia', label: 'Asistencia', icon: 'fact_check', roles: ADMIN, grant: 'payroll_attendance' },
      { path: 'incidencias', label: 'Incidencias', icon: 'edit_note', roles: ADMIN, grant: 'payroll_incidents' },
      { path: 'prenomina', label: 'Prenómina', icon: 'payments', roles: ADMIN, grant: 'payroll_prepayroll' },
      { path: 'clientes-nomina', label: 'Clientes (nómina)', icon: 'business_center', roles: ADMIN, grant: 'payroll_prepayroll' },
      { path: 'reportes-nomina', label: 'Reportes de nómina', icon: 'summarize', roles: ADMIN, grant: 'payroll_prepayroll' },
    ],
  },
  {
    label: 'Administración',
    items: [{ path: 'usuarios', label: 'Usuarios', icon: 'manage_accounts', roles: ADMIN, grant: 'users' }],
  },
];

export const ALL_NAV_ITEMS: readonly NavItem[] = NAVIGATION.flatMap((s) => s.items);

export function visibleNavigation(me: MeDto): NavSection[] {
  return NAVIGATION.map((section) => ({
    label: section.label,
    items: section.items.filter((item) => isVisible(item, me)),
  })).filter((section) => section.items.length > 0);
}

export function isVisible(item: NavItem, me: MeDto): boolean {
  if (!item.roles.includes(me.role)) return false;
  return !item.grant || me.role !== 'administrator' || me.grants.some((g) => g.module === item.grant);
}

/** First screen the user may open (a supervisor with only Asistencia lands on Asistencia). */
export function homePath(me: MeDto): string {
  return ALL_NAV_ITEMS.find((item) => isVisible(item, me))?.path ?? 'inicio';
}
