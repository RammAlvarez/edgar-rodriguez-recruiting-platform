import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { openSidePanel, Session } from '@platform/ui';
import type { RecruiterSummaryDto } from '../data/api-types';
import { RecruitersRepository } from '../data/recruiting-repositories';
import { UserPanel, type UserPanelData } from '../users/user-panel';

/** «% conversión» = contratados / presentados, truncated to a whole percent (no rounding anywhere, PLAN D42). */
export function conversion(r: Pick<RecruiterSummaryDto, 'candidatesPresented' | 'candidatesHired'>): string {
  const presented = Number(r.candidatesPresented);
  return presented === 0 ? '—' : `${Math.trunc((Number(r.candidatesHired) * 100) / presented)} %`;
}

/** Reclutamiento → Reclutadores (SPEC §4.4, ARCHITECTURE §9 2c): a card per recruiter with her live KPIs. */
@Component({
  selector: 'app-recruiters-page',
  imports: [MatButton, MatIcon, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Reclutadores</h1>
        @if (canInvite) {
          <button matButton="filled" type="button" (click)="invite()">
            <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Invitar reclutador
          </button>
        }
      </header>
      @if (recruiters.loading() && !recruiters.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (recruiters.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="cards">
        @for (r of recruiters.value() ?? []; track r.id) {
          <button type="button" class="card" (click)="go(r.id)">
            <div class="head">
              <span class="avatar" aria-hidden="true">{{ initials(r.displayName) }}</span>
              <div class="who">
                <div class="name">
                  {{ r.displayName }}
                  @if (!r.isActive) {
                    <span class="pill">Inactivo</span>
                  }
                </div>
                <div class="muted">{{ r.openingsAssigned }} {{ +r.openingsAssigned === 1 ? 'vacante asignada' : 'vacantes asignadas' }}</div>
              </div>
            </div>
            <div class="stats">
              <div><span class="value">{{ r.candidatesPresented }}</span><span class="label">Presentados</span></div>
              <div><span class="value">{{ r.candidatesHired }}</span><span class="label">Contratados</span></div>
              <div><span class="value conv">{{ conversion(r) }}</span><span class="label">Conversión</span></div>
            </div>
          </button>
        } @empty {
          <p class="muted">Sin reclutadores. Invítalos desde aquí o desde Usuarios.</p>
        }
      </div>
    </div>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }
    .card {
      all: unset;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      cursor: pointer;
      &:hover {
        background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent);
      }
      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
      }
    }
    .head {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .avatar {
      display: grid;
      place-items: center;
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      font: var(--mat-sys-label-large);
    }
    .name {
      display: flex;
      align-items: center;
      gap: 8px;
      font: var(--mat-sys-title-medium);
    }
    .stats {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      text-align: center;
      gap: 8px;
      margin-top: 12px;
      div {
        display: flex;
        flex-direction: column;
      }
      .value {
        font: var(--mat-sys-title-large);
        font-variant-numeric: tabular-nums;
      }
      .conv {
        color: var(--mat-sys-primary);
      }
      .label {
        font: var(--mat-sys-label-small);
        color: var(--mat-sys-on-surface-variant);
      }
    }
  `,
})
export class RecruitersPage {
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly session = inject(Session);
  readonly recruiters = inject(RecruitersRepository).list();
  readonly conversion = conversion;

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((w) => w[0]!.toUpperCase())
      .join('');
  }
  /** Inviting is Usuarios' job: only with that grant. */
  readonly canInvite = this.session.hasGrant('users');

  go(id: string): void {
    void this.router.navigate(['/reclutadores', id]);
  }

  invite(): void {
    openSidePanel<UserPanel, UserPanelData>(this.dialog, UserPanel, { user: null, clients: [], myId: this.session.me()?.id ?? '' });
  }
}
