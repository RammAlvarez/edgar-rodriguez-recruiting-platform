import type { Routes } from '@angular/router';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { type TopNavItem } from '@platform/ui/top-nav-shell';

// Cliente users only; the API scopes every query to their client (DATA_MODEL §8, PLAN D78).
const NAV: readonly TopNavItem[] = [
  { path: 'vacantes', label: 'Mis vacantes' },
  { path: 'comentarios', label: 'Comentarios' },
];

const signIn = () => import('@platform/ui/email-link-sign-in').then((m) => m.EmailLinkSignIn);

export const routes: Routes = [
  { path: 'entrar', loadComponent: signIn, canActivate: [signedOutGuard], data: { appName: 'Portal de clientes' }, title: 'Entrar' },
  { path: 'entrar/enlace', loadComponent: signIn, data: { appName: 'Portal de clientes' }, title: 'Entrar' },
  { path: 'sin-acceso', loadComponent: () => import('@platform/ui/status-pages').then((m) => m.NoAccessPage), title: 'Sin acceso' },
  {
    path: '',
    loadComponent: () => import('@platform/ui/top-nav-shell').then((m) => m.TopNavShell),
    canActivate: [signedInGuard, accessGuard(['client'])],
    data: { title: 'Portal de clientes', items: NAV },
    children: [
      { path: '', pathMatch: 'full', redirectTo: NAV[0].path },
      { path: 'vacantes', loadComponent: () => import('./openings/openings-page').then((m) => m.OpeningsPage), title: 'Mis vacantes' },
      { path: 'vacantes/:id', loadComponent: () => import('./openings/opening-detail').then((m) => m.OpeningDetail), title: 'Vacante' },
      { path: 'comentarios', loadComponent: () => import('./comments/comments-page').then((m) => m.CommentsPage), title: 'Comentarios' },
    ],
  },
  { path: '**', redirectTo: '' },
];
