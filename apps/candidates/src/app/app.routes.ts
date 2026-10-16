import type { Routes } from '@angular/router';
import { accessGuard, signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { type TopNavItem } from '@platform/ui/top-nav-shell';
import { privacyAcceptedGuard } from './privacy/privacy-guard';

// Candidates have no users row: the API recognises them by their verified email (DATA_MODEL §8, PLAN D77).
const NAV: readonly TopNavItem[] = [
  { path: 'perfil', label: 'Mi perfil' },
  { path: 'documentos', label: 'Mis documentos' },
  { path: 'postulacion', label: 'Mi postulación' },
];

const signIn = () => import('@platform/ui/email-link-sign-in').then((m) => m.EmailLinkSignIn);

export const routes: Routes = [
  { path: 'entrar', loadComponent: signIn, canActivate: [signedOutGuard], data: { appName: 'Mi postulación' }, title: 'Entrar' },
  { path: 'entrar/enlace', loadComponent: signIn, data: { appName: 'Mi postulación' }, title: 'Entrar' },
  { path: 'sin-acceso', loadComponent: () => import('@platform/ui/status-pages').then((m) => m.NoAccessPage), title: 'Sin acceso' },
  {
    path: 'aviso',
    loadComponent: () => import('./privacy/privacy-page').then((m) => m.PrivacyPage),
    canActivate: [signedInGuard, accessGuard(['candidate'])],
    title: 'Aviso de privacidad',
  },
  {
    path: '',
    loadComponent: () => import('@platform/ui/top-nav-shell').then((m) => m.TopNavShell),
    canActivate: [signedInGuard, accessGuard(['candidate'])],
    canActivateChild: [privacyAcceptedGuard],
    data: { title: 'Mi postulación', items: NAV },
    children: [
      { path: '', pathMatch: 'full', redirectTo: NAV[0].path },
      { path: 'perfil', loadComponent: () => import('./profile/profile-page').then((m) => m.ProfilePage), title: 'Mi perfil' },
      { path: 'documentos', loadComponent: () => import('./documents/documents-page').then((m) => m.DocumentsPage), title: 'Mis documentos' },
      { path: 'postulacion', loadComponent: () => import('./application/application-page').then((m) => m.ApplicationPage), title: 'Mi postulación' },
    ],
  },
  { path: '**', redirectTo: '' },
];
