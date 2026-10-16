import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconButton } from '@angular/material/button';
import { MatDivider } from '@angular/material/divider';
import { MatIcon } from '@angular/material/icon';
import { MatListItem, MatListItemIcon, MatListItemTitle, MatNavList } from '@angular/material/list';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { MatToolbar } from '@angular/material/toolbar';
import { MatTooltip } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { RefreshButton, Session } from '@platform/ui';
import { map } from 'rxjs';
import { visibleNavigation } from './navigation';

const ROLE_LABELS = { administrator: 'Administrador', recruiter: 'Reclutador', client: 'Cliente' } as const;

/** Desktop-first shell: fixed sidenav ≥ 960px, modal below (design system "Layout"). */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavContainer,
    MatSidenav,
    MatSidenavContent,
    MatToolbar,
    MatNavList,
    MatListItem,
    MatListItemIcon,
    MatListItemTitle,
    MatIcon,
    MatIconButton,
    MatMenu,
    MatMenuItem,
    MatMenuTrigger,
    MatDivider,
    MatTooltip,
    RefreshButton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './shell.scss',
  templateUrl: './shell.html',
})
export class Shell {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 959.98px)')
      .pipe(map((r) => r.matches)),
    { initialValue: false },
  );
  readonly me = this.session.me;
  readonly sections = computed(() => {
    const me = this.me();
    return me ? visibleNavigation(me) : [];
  });
  readonly userLabel = computed(() => {
    const me = this.me();
    return me ? `${me.displayName} · ${ROLE_LABELS[me.role]}` : '';
  });

  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }
}
