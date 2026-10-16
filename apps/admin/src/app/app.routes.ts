import { inject, type Type } from '@angular/core';
import { type CanActivateFn, Router, type Routes } from '@angular/router';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { Session } from '@platform/ui/session';
import { ALL_NAV_ITEMS, homePath } from './shell/navigation';

/** Screens delivered so far (S3); the other sections keep the «próxima entrega» placeholder. */
const SCREENS: Record<string, () => Promise<Type<unknown>>> = {
  usuarios: () => import('./users/users-page').then((m) => m.UsersPage),
  clientes: () => import('./clients/clients-page').then((m) => m.ClientsPage),
  'clientes-nomina': () => import('./clients/payroll-clients-page').then((m) => m.PayrollClientsPage),
  catalogos: () => import('./catalogs/catalogs-page').then((m) => m.CatalogsPage),
};

const navItem = (path: string) => ALL_NAV_ITEMS.find((i) => i.path === path)!;

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
        loadComponent: SCREENS[item.path] ?? (() => import('@platform/ui/status-pages').then((m) => m.ComingSoonPage)),
        canActivate: [accessGuard(item.roles, item.grant)],
        data: { title: item.label, icon: item.icon },
        title: item.label,
      })),
      {
        path: 'clientes/:id',
        loadComponent: () => import('./clients/client-detail').then((m) => m.ClientDetail),
        canActivate: [accessGuard(navItem('clientes').roles)],
        title: 'Cliente',
      },
      {
        path: 'clientes-nomina/:id',
        loadComponent: () => import('./clients/payroll-client-detail').then((m) => m.PayrollClientDetail),
        canActivate: [accessGuard(navItem('clientes-nomina').roles, navItem('clientes-nomina').grant)],
        title: 'Cliente (nómina)',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
