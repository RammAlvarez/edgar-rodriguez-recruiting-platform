import { inject, type Type } from '@angular/core';
import { type CanActivateFn, Router, type Routes } from '@angular/router';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { Session } from '@platform/ui/session';
import { CoverMode } from './shell/cover-mode';
import { ALL_NAV_ITEMS, homePath } from './shell/navigation';

/** Screens delivered so far (S4); the other sections keep the «próxima entrega» placeholder. */
const SCREENS: Record<string, () => Promise<Type<unknown>>> = {
  vacantes: () => import('./openings/openings-page').then((m) => m.OpeningsPage),
  'mis-vacantes': () => import('./openings/openings-page').then((m) => m.OpeningsPage),
  candidatos: () => import('./candidates/candidates-page').then((m) => m.CandidatesPage),
  'mis-candidatos': () => import('./candidates/candidates-page').then((m) => m.CandidatesPage),
  reclutadores: () => import('./recruiters/recruiters-page').then((m) => m.RecruitersPage),
  usuarios: () => import('./users/users-page').then((m) => m.UsersPage),
  clientes: () => import('./clients/clients-page').then((m) => m.ClientsPage),
  'clientes-nomina': () => import('./clients/payroll-clients-page').then((m) => m.PayrollClientsPage),
  catalogos: () => import('./catalogs/catalogs-page').then((m) => m.CatalogsPage),
};

const navItem = (path: string) => ALL_NAV_ITEMS.find((i) => i.path === path)!;

const homeRedirect: CanActivateFn = () => {
  const me = inject(Session).me();
  const home = me ? homePath(me, !!inject(CoverMode).active()) : null;
  return inject(Router).createUrlTree([home ?? (me ? '/sin-acceso' : '/entrar')]);
};

/** «Mis vacantes» / «Mis candidatos»: a Reclutador, or an Administrador covering one (SPEC §4.4). */
const workspaceGuard: CanActivateFn = () => {
  const me = inject(Session).me();
  if (me?.role === 'recruiter' || (me?.role === 'administrator' && inject(CoverMode).active())) return true;
  return inject(Router).createUrlTree([me ? '/' : '/entrar']);
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
        canActivate: [item.path.startsWith('mis-') ? workspaceGuard : accessGuard(item.roles, item.grant)],
        // «Mis vacantes» / «Mis candidatos»: the recruiter workspace, the same screens with the Administrador actions hidden.
        data: { title: item.label, icon: item.icon, workspace: item.path.startsWith('mis-') },
        title: item.label,
      })),
      {
        path: 'clientes/:id',
        loadComponent: () => import('./clients/client-detail').then((m) => m.ClientDetail),
        canActivate: [accessGuard(navItem('clientes').roles, navItem('clientes').grant)],
        title: 'Cliente',
      },
      {
        path: 'vacantes/:id',
        loadComponent: () => import('./openings/opening-detail').then((m) => m.OpeningDetail),
        canActivate: [accessGuard(navItem('vacantes').roles, navItem('vacantes').grant)],
        title: 'Vacante',
      },
      {
        path: 'mis-vacantes/:id',
        loadComponent: () => import('./openings/opening-detail').then((m) => m.OpeningDetail),
        canActivate: [workspaceGuard],
        data: { workspace: true },
        title: 'Vacante',
      },
      {
        path: 'candidatos/:id',
        loadComponent: () => import('./candidates/candidate-detail').then((m) => m.CandidateDetail),
        canActivate: [accessGuard(navItem('candidatos').roles, navItem('candidatos').grant)],
        title: 'Candidato',
      },
      {
        path: 'mis-candidatos/:id',
        loadComponent: () => import('./candidates/candidate-detail').then((m) => m.CandidateDetail),
        canActivate: [workspaceGuard],
        data: { workspace: true },
        title: 'Candidato',
      },
      {
        path: 'reclutadores/:id',
        loadComponent: () => import('./recruiters/recruiter-detail').then((m) => m.RecruiterDetail),
        canActivate: [accessGuard(navItem('reclutadores').roles, navItem('reclutadores').grant)],
        title: 'Reclutador',
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
