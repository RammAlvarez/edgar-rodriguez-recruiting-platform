import type { ActivatedRouteSnapshot, Route } from '@angular/router';
import { ParamAwareRouteReuseStrategy } from './route-reuse';

const snap = (routeConfig: Route, params: Record<string, string>) => ({ routeConfig, params }) as unknown as ActivatedRouteSnapshot;

describe('ParamAwareRouteReuseStrategy', () => {
  const strategy = new ParamAwareRouteReuseStrategy();
  const detail: Route = { path: 'vacantes/:id' };

  it('builds a new screen when the id changes (Duplicar vacante)', () => {
    expect(strategy.shouldReuseRoute(snap(detail, { id: 'copy' }), snap(detail, { id: 'original' }))).toBe(false);
  });

  it('reuses the screen for the same route and id', () => {
    expect(strategy.shouldReuseRoute(snap(detail, { id: 'a' }), snap(detail, { id: 'a' }))).toBe(true);
  });

  it('never reuses across routes', () => {
    expect(strategy.shouldReuseRoute(snap({ path: 'x' }, {}), snap({ path: 'y' }, {}))).toBe(false);
  });
});
