import type { PermissionModule } from '@platform/shared';
import type { GrantDto } from '../data/api-types';
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
