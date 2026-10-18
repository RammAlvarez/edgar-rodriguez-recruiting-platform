import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTabLink, MatTabNav, MatTabNavPanel } from '@angular/material/tabs';
import { MatToolbar } from '@angular/material/toolbar';
import { MatTooltip } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { RefreshButton } from './refresh-button';
import { Session } from './session';
import { ThemePicker } from './theme';

export interface TopNavItem {
  readonly path: string;
  readonly label: string;
}

/** Mobile-first shell of the candidates and clients apps: a toolbar and 2–3 top tabs (prototype "simple top navigation"). */
@Component({
  selector: 'ui-top-nav-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbar, MatTabNav, MatTabLink, MatTabNavPanel, MatIconButton, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatTooltip, RefreshButton, ThemePicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100%; }
    .toolbar { gap: 4px; background: var(--mat-sys-surface); }
    .brand { color: var(--mat-sys-primary); margin-right: 4px; }
    .title { flex: 1; font: var(--mat-sys-title-medium); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    nav { background: var(--mat-sys-surface); border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .user { padding: 12px 16px; font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    .theme { display: flex; flex-direction: column; gap: 8px; padding: 8px 16px 12px; font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    main { flex: 1; }
  `,
  template: `
    <mat-toolbar class="toolbar">
      <mat-icon class="brand" fontSet="material-symbols-outlined">badge</mat-icon>
      <span class="title">{{ title() }}</span>
      <ui-refresh-button />
      <button matIconButton type="button" [matMenuTriggerFor]="menu" aria-label="Cuenta" matTooltip="Cuenta">
        <mat-icon fontSet="material-symbols-outlined">account_circle</mat-icon>
      </button>
      <mat-menu #menu="matMenu" xPosition="before">
        <div class="user">{{ session.me()?.displayName }}</div>
        <div class="theme" (click)="$event.stopPropagation()" (keydown)="$event.stopPropagation()" tabindex="-1">
          Tema
          <ui-theme-picker />
        </div>
        <button mat-menu-item type="button" (click)="signOut()">
          <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
          <span>Cerrar sesión</span>
        </button>
      </mat-menu>
    </mat-toolbar>
    <nav mat-tab-nav-bar [tabPanel]="panel">
      @for (item of items(); track item.path) {
        <a mat-tab-link [routerLink]="item.path" routerLinkActive #rla="routerLinkActive" [active]="rla.isActive">{{ item.label }}</a>
      }
    </nav>
    <mat-tab-nav-panel #panel>
      <main><router-outlet /></main>
    </mat-tab-nav-panel>
  `,
})
export class TopNavShell {
  readonly session = inject(Session);
  private readonly router = inject(Router);

  readonly title = input.required<string>();
  readonly items = input.required<readonly TopNavItem[]>();

  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }
}
