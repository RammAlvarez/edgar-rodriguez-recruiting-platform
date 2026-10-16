import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TimestampPipe } from '@platform/ui/pipes';
import { ClientPortal } from '../data/portal';

/**
 * «Comentarios» (prototype top nav, PLAN D78): the latest messages of every vacante, newest first; each opens its
 * vacante, where the thread is answered.
 */
@Component({
  selector: 'app-comments-page',
  imports: [RouterLink, MatIcon, MatProgressBar, TimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      <header class="page-header"><h1>Comentarios</h1></header>
      @if (feed.loading() && !feed.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (feed.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="list-card">
        @for (c of feed.value() ?? []; track c.id) {
          <a class="list-row clickable" [routerLink]="['/vacantes', c.openingId]">
            <div class="grow">
              <div class="opening">{{ c.openingName }}</div>
              <div class="body">{{ c.body }}</div>
              <div class="muted">{{ c.fromClient ? 'Tú' : c.author }} · {{ c.createdAt | timestamp }}</div>
            </div>
            <mat-icon fontSet="material-symbols-outlined" class="muted">chevron_right</mat-icon>
          </a>
        } @empty {
          @if (feed.value()) {
            <p class="list-empty">Aún no hay comentarios. Abre una vacante para escribir a tu reclutador.</p>
          }
        }
      </div>
    </div>
  `,
  styles: `
    .narrow {
      max-width: 760px;
    }
    a.list-row {
      color: inherit;
      text-decoration: none;
    }
    .grow {
      flex: 1;
      min-width: 0;
    }
    .opening {
      font-weight: 500;
    }
    .body {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class CommentsPage {
  readonly feed = inject(ClientPortal).feed();
}
