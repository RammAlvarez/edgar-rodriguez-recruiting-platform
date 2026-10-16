import type { Routes } from '@angular/router';
import { signedInGuard, signedOutGuard } from '@platform/ui/guards';
import { type TopNavItem } from '@platform/ui/top-nav-shell';

// Candidates have no users row: their principal (matched by verified email, DATA_MODEL §8) arrives in slice S5.
const NAV: readonly TopNavItem[] = [
  { path: 'perfil', label: 'Mi perfil' },
  { path: 'documentos', label: 'Mis documentos' },
  { path: 'postulacion', label: 'Mi postulación' },
];
const ICONS: Record<string, string> = { perfil: "person", documentos: "folder", postulacion: "assignment" };

const signIn = () => import('@platform/ui/email-link-sign-in').then((m) => m.EmailLinkSignIn);

export const routes: Routes = [
  { path: 'entrar', loadComponent: signIn, canActivate: [signedOutGuard], data: { appName: 'Mi postulación' }, title: 'Entrar' },
  { path: 'entrar/enlace', loadComponent: signIn, data: { appName: 'Mi postulación' }, title: 'Entrar' },
  { path: 'sin-acceso', loadComponent: () => import('@platform/ui/status-pages').then((m) => m.NoAccessPage), title: 'Sin acceso' },
  {
    path: '',
    loadComponent: () => import('@platform/ui/top-nav-shell').then((m) => m.TopNavShell),
    canActivate: [signedInGuard],
    data: { title: 'Mi postulación', items: NAV },
    children: [
      { path: '', pathMatch: 'full', redirectTo: NAV[0].path },
      ...NAV.map((item) => ({
        path: item.path,
        loadComponent: () => import('@platform/ui/status-pages').then((m) => m.ComingSoonPage),
        data: { title: item.label, icon: ICONS[item.path] },
        title: item.label,
      })),
    ],
  },
  { path: '**', redirectTo: '' },
];
