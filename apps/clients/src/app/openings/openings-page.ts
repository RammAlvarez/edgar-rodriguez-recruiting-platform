import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ClientPortal, countOf, FUNNEL, OPENING_STATUS, type ClientOpeningDto } from '../data/portal';

/**
 * «Mis vacantes» (SPEC §4.6): a card per vacante with estatus, contratados de solicitadas, días abierta, the líder with phone
 * and the candidates per etapa. Never the vacante's Sexo or Edad (D22) — the API does not send them.
 */
@Component({
  selector: 'app-openings-page',
  imports: [RouterLink, MatIcon, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Mis vacantes</h1></header>
      @if (openings.loading() && !openings.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (openings.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="cards">
        @for (o of openings.value() ?? []; track o.id) {
          <a class="card" [routerLink]="['/vacantes', o.id]">
            <div class="top">
              <h2>{{ o.displayName }}</h2>
              <span class="pill" [class]="status(o).tone">{{ status(o).label }}</span>
            </div>
            <div class="line"><strong>{{ o.hired }} de {{ o.headcountRequested }} contratados</strong> · {{ o.daysOpen }} días abierta</div>
            @if (o.leadRecruiterName) {
              <div class="muted">Reclutador líder: {{ o.leadRecruiterName }}@if (o.leadRecruiterPhone) { · {{ o.leadRecruiterPhone }} }</div>
            }
            <div class="funnel" aria-label="Candidatos por etapa">
              @for (f of funnel; track f.stage) {
                <div class="step">
                  <div class="count">{{ count(o, f.stage) }}</div>
                  <div class="label">{{ f.label }}</div>
                </div>
              }
            </div>
          </a>
        } @empty {
          @if (openings.value()) {
            <div class="empty-state">
              <mat-icon fontSet="material-symbols-outlined">work</mat-icon>
              <p>Aún no hay vacantes publicadas en tu portal.</p>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(320px, 100%), 1fr));
      gap: 16px;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface);
      color: inherit;
      text-decoration: none;
      &:hover,
      &:focus-visible {
        border-color: var(--mat-sys-primary);
      }
    }
    .top {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 8px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-medium);
      }
    }
    .funnel {
      display: grid;
      grid-template-columns: repeat(5, 1fr);
      gap: 4px;
      text-align: center;
      .step {
        padding: 6px 4px;
        border-radius: 8px;
        background: var(--mat-sys-surface-container);
      }
      .count {
        font: var(--mat-sys-title-medium);
      }
      .label {
        font: var(--mat-sys-label-small);
        color: var(--mat-sys-on-surface-variant);
      }
    }
  `,
})
export class OpeningsPage {
  readonly openings = inject(ClientPortal).openings();
  readonly funnel = FUNNEL;
  readonly count = countOf;

  status(o: ClientOpeningDto) {
    return OPENING_STATUS[o.status];
  }
}
