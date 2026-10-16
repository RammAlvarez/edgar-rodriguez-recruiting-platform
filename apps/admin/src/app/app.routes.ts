import { inject } from '@angular/core';
import { type CanActivateFn, Router, type Routes } from '@angular/router';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { Session } from '@platform/ui/session';
import { ALL_NAV_ITEMS, homePath } from './shell/navigation';

const homeRedirect: CanActivateFn = () => {
  const me = inject(Session).me();
  const home = me ? homePath(me) : null;
  return inject(Router).createUrlTree([home ?? (me ? '/sin-acceso' : '/entrar')]);
};

export const routes: Routes = [
  { path: 'entrar', loadComponent: () => import('./sign-in/sign-in').then((m) => m.SignIn), canActivate: [signedOutGuard], title: 'Entrar' },
  { path: 'sin-acceso', loadComponent: () => import('@platform/ui/status-pages').then((m) => m.NoAccessPage), title: 'Sin acceso' },
  {
    path: '',
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    canActivate: [signedInGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [homeRedirect], children: [] },
      // Every section of the sidebar; each slice of docs/PLAN.md replaces its placeholder with the real screen.
      ...ALL_NAV_ITEMS.map((item) => ({
        path: item.path,
        loadComponent: () => import('@platform/ui/status-pages').then((m) => m.ComingSoonPage),
        canActivate: [accessGuard(item.roles, item.grant)],
        data: { title: item.label, icon: item.icon },
        title: item.label,
      })),
    ],
  },
  { path: '**', redirectTo: '' },
];
