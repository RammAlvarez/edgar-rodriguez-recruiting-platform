import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatBadge } from '@angular/material/badge';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDivider } from '@angular/material/divider';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTooltip } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import type { NotificationDto } from '../data/api-types';
import { NotificationsRepository } from '../data/notifications-repository';

const ICON = {
  document_expiring: 'event_busy',
  document_uploaded: 'upload_file',
  opening_idle: 'hourglass_bottom',
  attendance_missing: 'person_off',
  period_in_review: 'rule',
  user_invited: 'waving_hand',
} as const;

/** How long the list waits before asking again (the API also invalidates it after each change of the user's own). */
const REFRESH_MS = 60_000;

/**
 * The bell (SPEC §3.5, PLAN D88): the unread count, the latest items — opening one goes to its screen and marks it read — and
 * «Marcar todas como leídas». The daily email digest carries the same items.
 */
@Component({
  selector: 'app-notification-bell',
  imports: [MatBadge, MatButton, MatIconButton, MatDivider, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatTooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button matIconButton type="button" [matMenuTriggerFor]="menu" (menuOpened)="list.reload()" aria-label="Avisos" matTooltip="Avisos">
      <mat-icon fontSet="material-symbols-outlined" [matBadge]="unread() || null" matBadgeSize="small" matBadgeColor="warn"
        [attr.aria-label]="unread() ? unread() + ' sin leer' : null">notifications</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before" class="bell-menu">
      <div class="head">
        <strong>Avisos</strong>
        @if (unread()) {
          <button matButton type="button" (click)="$event.stopPropagation(); readAll()">Marcar todas como leídas</button>
        }
      </div>
      <mat-divider />
      @for (n of list.value()?.items ?? []; track n.id) {
        <button mat-menu-item type="button" class="item" [class.unread]="!n.read" (click)="open(n)">
          <mat-icon fontSet="material-symbols-outlined">{{ icon[n.kind!] }}</mat-icon>
          <span class="text">
            <span class="title">{{ n.title }}</span>
            @if (n.body) {
              <span class="body">{{ n.body }}</span>
            }
            <span class="when">{{ when(n.createdAt) }}</span>
          </span>
        </button>
      } @empty {
        <p class="empty">Sin avisos.</p>
      }
    </mat-menu>
  `,
  styles: `
    :host {
      display: contents;
    }
    .head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 8px 16px;
    }
    .item {
      height: auto;
      min-height: 64px;
      line-height: normal;
      padding-top: 8px;
      padding-bottom: 8px;
      white-space: normal;
      &.unread .title {
        font-weight: 600;
      }
    }
    .text {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .body,
    .when {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .empty {
      padding: 16px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NotificationBell {
  private readonly repo = inject(NotificationsRepository);
  private readonly router = inject(Router);
  readonly icon = ICON;
  readonly list = this.repo.list();

  constructor() {
    const timer = setInterval(() => this.list.reload(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  unread(): number {
    return Number(this.list.value()?.unread ?? 0);
  }

  when(at: string | Date): string {
    return new Date(at).toLocaleString('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Monterrey' });
  }

  async open(n: NotificationDto): Promise<void> {
    if (!n.read) await this.repo.read(n.id).catch(() => undefined);
    if (n.link) await this.router.navigateByUrl(n.link);
  }

  async readAll(): Promise<void> {
    await this.repo.readAll();
  }
}
