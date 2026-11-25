import { type ActivatedRouteSnapshot, BaseRouteReuseStrategy } from '@angular/router';

/**
 * A detail screen reads its `:id` once (the live queries start in field initializers), so going from one vacante to
 * another on the same route must build a new screen. Angular reuses it by default: «Duplicar vacante» showed the copy's
 * URL with the original's data, and «Editar» then saved the original (demo run, 2026-11-25).
 */
export class ParamAwareRouteReuseStrategy extends BaseRouteReuseStrategy {
  override shouldReuseRoute(future: ActivatedRouteSnapshot, current: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === current.routeConfig && sameParams(future.params, current.params);
  }
}

function sameParams(a: Record<string, string>, b: Record<string, string>): boolean {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((k) => a[k] === b[k]);
}
