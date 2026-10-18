import type { PermissionModule } from '@platform/shared';
import type { GrantDto, UserDto } from '../data/api-types';
import { GRANTS } from '../data/labels';

/**
 * The panel edits grants as checkboxes plus one optional «Solo para el cliente» (ARCHITECTURE §9.12). Reclutamiento,
 * Usuarios and Reportes always apply to all clients (PLAN D67), so the scope is never put on them.
 */
export function toGrants(modules: readonly PermissionModule[], onlyClientId: string | null): GrantDto[] {
  return modules.map((module) => ({
    module,
    clientId: GRANTS.find((g) => g.module === module)?.allClientsOnly ? null : onlyClientId,
  }));
}

/** Back from the API: the checked modules and the single client scope, or `mixed` when grants have different scopes. */
export function fromGrants(grants: readonly GrantDto[]): { modules: PermissionModule[]; onlyClientId: string | null; mixed: boolean } {
  const scopes = new Set(
    grants.filter((g) => !GRANTS.find((x) => x.module === g.module)?.allClientsOnly).map((g) => g.clientId ?? null),
  );
  return {
    modules: [...new Set(grants.map((g) => g.module))],
    onlyClientId: scopes.size === 1 ? [...scopes][0]! : null,
    mixed: scopes.size > 1,
  };
}

const ALL_CLIENTS = 'todos los clientes';

/**
 * «Cliente» column of Usuarios: the one client of a Cliente user, the client an Administrador is limited to («Todos» when none),
 * `—` for a Reclutador. An Administrador whose permissions have different limits reads «Según permiso» (the panel lists them).
 */
export function clientColumn(user: UserDto, clientName: (id: string) => string): string {
  if (user.role === 'client') return user.clientName ?? '—';
  if (user.role !== 'administrator') return '—';
  const scopes = new Set(user.grants.filter((g) => !GRANTS.find((x) => x.module === g.module)?.allClientsOnly).map((g) => g.clientId ?? null));
  if (scopes.size === 0 || (scopes.size === 1 && scopes.has(null))) return 'Todos';
  if (scopes.size === 1) return clientName([...scopes][0]!);
  return 'Según permiso';
}

/** «Acceso: Asistencia — solo E2E LIMPIEZA · Usuarios — todos los clientes»: what the user can reach and over which clients. */
export function accessSummary(user: UserDto, clientName: (id: string) => string): string {
  if (user.role === 'client') return `Portal de clientes — solo ${user.clientName ?? 'su cliente'}`;
  if (user.role === 'recruiter') return 'Reclutamiento — las vacantes que le asignen';
  const groups = new Map<string, string[]>();
  for (const g of user.grants) {
    const option = GRANTS.find((x) => x.module === g.module);
    const scope = option?.allClientsOnly || !g.clientId ? ALL_CLIENTS : `solo ${clientName(g.clientId)}`;
    groups.set(scope, [...(groups.get(scope) ?? []), option?.label ?? g.module]);
  }
  return [...groups].map(([scope, labels]) => `${labels.join(', ')} — ${scope}`).join(' · ') || 'Sin permisos delegados';
}
