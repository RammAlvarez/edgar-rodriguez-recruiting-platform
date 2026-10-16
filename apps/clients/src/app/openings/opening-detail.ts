import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DateKeyPipe, MoneyPipe, TimestampPipe } from '@platform/ui/pipes';
import { ClientPortal, OPENING_STATUS, SOURCE, STAGE, type ClientCandidateDto } from '../data/portal';
import { CommentThread } from './comment-thread';

/**
 * Vacante detail of the portal (SPEC §4.6): the candidate list — before Apto only initials, fuente, etapa and fecha de
 * captura; from Apto the full name, sueldo ofrecido, fecha de entrevista and documents completeness — and the comment thread.
 */
@Component({
  selector: 'app-opening-detail',
  imports: [RouterLink, MatButton, MatIcon, MatProgressBar, DateKeyPipe, MoneyPipe, TimestampPipe, CommentThread],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton routerLink="/vacantes" class="back"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>Mis vacantes</a>
      @if (detail.loading() && !detail.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (detail.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (detail.value(); as d) {
        <header class="page-header">
          <h1>
            {{ d.opening.displayName }}
            <span class="subtitle">Solicitada el {{ d.opening.requestedAt | dateKey }} · {{ d.opening.daysOpen }} días abierta</span>
          </h1>
          <span class="pill" [class]="statusOf(d.opening.status).tone">{{ statusOf(d.opening.status).label }}</span>
        </header>
        <div class="kpis">
          <div class="kpi"><div class="label">Contratados</div><div class="value">{{ d.opening.hired }} de {{ d.opening.headcountRequested }}</div></div>
          <div class="kpi"><div class="label">Candidatos</div><div class="value">{{ d.candidates.length }}</div></div>
          @if (d.opening.leadRecruiterName) {
            <div class="kpi">
              <div class="label">Reclutador líder</div>
              <div class="lead">{{ d.opening.leadRecruiterName }}</div>
              @if (d.opening.leadRecruiterPhone) {
                <a [href]="'tel:' + d.opening.leadRecruiterPhone">{{ d.opening.leadRecruiterPhone }}</a>
              }
            </div>
          }
        </div>
        <div class="layout">
          <section>
            <h2 class="section">Candidatos</h2>
            <div class="list-card">
              @for (c of d.candidates; track c.id) {
                <div class="candidate">
                  <div class="who">
                    <div class="name" [class.masked]="!c.isIdentified">{{ c.name }}</div>
                    <div class="muted">
                      {{ sourceOf(c) }} · capturado el {{ c.capturedAt | dateKey }}
                    </div>
                    @if (c.isIdentified) {
                      <div class="facts">
                        @if (c.offeredSalary) {
                          <span>Sueldo ofrecido: <strong>{{ c.offeredSalary | money: true }}</strong></span>
                        }
                        @if (c.interviewAt) {
                          <span>Entrevista: <strong>{{ c.interviewAt | timestamp }}</strong></span>
                        }
                        @if (c.documentsCompleteness !== null) {
                          <span>Documentos: <strong>{{ percent(c) }} %</strong></span>
                        }
                      </div>
                    }
                  </div>
                  <span class="pill" [class]="stageOf(c).tone">{{ stageOf(c).label }}</span>
                </div>
              } @empty {
                <p class="list-empty">Aún no hay candidatos para esta vacante.</p>
              }
            </div>
            <p class="muted note">Los nombres aparecen cuando el candidato es Apto; antes solo se muestran sus iniciales.</p>
          </section>
          <section>
            <h2 class="section">Comentarios</h2>
            <app-comment-thread />
          </section>
        </div>
      }
    </div>
  `,
  styles: `
    .section {
      margin: 8px 0 12px;
      font: var(--mat-sys-title-medium);
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
      gap: 24px;
      @media (max-width: 840px) {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .lead {
      font: var(--mat-sys-title-small);
    }
    .candidate {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      &:last-child {
        border-bottom: 0;
      }
      .name {
        font-weight: 500;
        &.masked {
          letter-spacing: 0.04em;
        }
      }
      .facts {
        display: flex;
        flex-wrap: wrap;
        gap: 4px 16px;
        margin-top: 4px;
      }
    }
    .note {
      margin-top: 8px;
      font: var(--mat-sys-body-small);
    }
  `,
})
export class OpeningDetail {
  /** Route param, read from the snapshot: the live query starts in the field initializer, before inputs are bound. */
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly detail = inject(ClientPortal).opening(() => this.id);

  statusOf(s: keyof typeof OPENING_STATUS) {
    return OPENING_STATUS[s];
  }

  stageOf(c: ClientCandidateDto) {
    return STAGE[c.stage];
  }

  sourceOf(c: ClientCandidateDto): string {
    return c.source ? SOURCE[c.source] : 'Sin fuente';
  }

  percent(c: ClientCandidateDto): string {
    return String(Math.trunc(Number(c.documentsCompleteness)));
  }
}
