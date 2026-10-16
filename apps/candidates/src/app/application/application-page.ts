import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { DateKeyPipe, TimestampPipe } from '@platform/ui/pipes';
import { type ApplicationStep, CandidatePortal, type CandidateApplicationDto } from '../data/portal';

export const STEPS: readonly { readonly step: ApplicationStep; readonly label: string }[] = [
  { step: 'received', label: 'Recibida' },
  { step: 'contacted', label: 'Contactado' },
  { step: 'interview', label: 'Entrevista' },
  { step: 'result', label: 'Resultado' },
];

/** The candidate-facing sentence for an application; reasons and internal stages are never shown (PLAN D77). */
export function statusMessage(a: CandidateApplicationDto): string {
  switch (a.outcome) {
    case 'selected':
      return 'Te seleccionamos para presentarte con la empresa. Tu reclutador te dirá los siguientes pasos.';
    case 'hired':
      return '¡Felicidades, te contrataron!';
    case 'closed':
      return 'Este proceso ya concluyó. Gracias por tu interés.';
    default:
      return a.step === 'interview'
        ? 'Estás en la etapa de entrevista.'
        : a.step === 'contacted'
          ? 'Tu reclutador ya está en contacto contigo.'
          : 'Recibimos tu postulación. Tu reclutador se pondrá en contacto contigo.';
  }
}

/** «Mi postulación» (SPEC §7, prototype): each application with a friendly tracker and the recruiter to contact. */
@Component({
  selector: 'app-application-page',
  imports: [MatIcon, MatProgressBar, DateKeyPipe, TimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      <header class="page-header"><h1>Mi postulación</h1></header>
      @if (apps.loading() && !apps.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (apps.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @for (a of apps.value() ?? []; track a.id) {
        <section class="card" [class.closed]="a.outcome === 'closed'">
          <h2>{{ a.openingName }}</h2>
          <div class="muted">Registrada el {{ a.capturedAt | dateKey }}</div>
          <ol class="tracker" [attr.aria-label]="'Avance: ' + labelOf(a)">
            @for (s of steps; track s.step; let i = $index) {
              <li [class.done]="i < indexOf(a)" [class.current]="i === indexOf(a)">
                <span class="dot">
                  @if (i < indexOf(a)) {
                    <mat-icon fontSet="material-symbols-outlined">check</mat-icon>
                  } @else {
                    {{ i + 1 }}
                  }
                </span>
                <span class="label">{{ s.label }}</span>
              </li>
            }
          </ol>
          <p class="message" [class.positive]="a.outcome === 'selected' || a.outcome === 'hired'">{{ message(a) }}</p>
          @if (a.outcome === 'hired' && a.hiredAt) {
            <p>Fecha de ingreso: <strong>{{ a.hiredAt | dateKey }}</strong></p>
          }
          @if (a.nextInterviewAt) {
            <p class="interview">
              <mat-icon fontSet="material-symbols-outlined">event</mat-icon>
              <span>Entrevista: <strong>{{ a.nextInterviewAt | timestamp }}</strong>@if (a.nextInterviewLocation) { · {{ a.nextInterviewLocation }} }</span>
            </p>
          }
          @if (a.recruiterName) {
            <div class="recruiter">
              <div class="muted">Tu reclutador</div>
              <div class="name">{{ a.recruiterName }}</div>
              @if (a.recruiterPhone) {
                <a [href]="'tel:' + a.recruiterPhone"><mat-icon fontSet="material-symbols-outlined">call</mat-icon>{{ a.recruiterPhone }}</a>
              }
              @if (a.recruiterEmail) {
                <a [href]="'mailto:' + a.recruiterEmail"><mat-icon fontSet="material-symbols-outlined">mail</mat-icon>{{ a.recruiterEmail }}</a>
              }
            </div>
          }
        </section>
      } @empty {
        @if (apps.value()) {
          <div class="empty-state">
            <mat-icon fontSet="material-symbols-outlined">assignment</mat-icon>
            <p>No tienes postulaciones registradas.</p>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .narrow {
      max-width: 720px;
    }
    .card {
      margin-bottom: 16px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface);
      h2 {
        margin: 0 0 2px;
        font: var(--mat-sys-title-medium);
      }
      &.closed {
        opacity: 0.8;
      }
    }
    .tracker {
      display: flex;
      gap: 4px;
      margin: 16px 0 8px;
      padding: 0;
      list-style: none;
      li {
        position: relative;
        display: flex;
        flex: 1;
        flex-direction: column;
        align-items: center;
        gap: 4px;
        min-width: 0;
        color: var(--mat-sys-on-surface-variant);
        &:not(:first-child)::before {
          content: '';
          position: absolute;
          top: 14px;
          right: calc(50% + 18px);
          left: calc(-50% + 18px);
          height: 2px;
          background: var(--mat-sys-outline-variant);
        }
        &.done::before,
        &.current::before {
          background: var(--mat-sys-primary);
        }
      }
      .dot {
        display: grid;
        place-items: center;
        width: 28px;
        height: 28px;
        border: 2px solid var(--mat-sys-outline-variant);
        border-radius: 50%;
        font: var(--mat-sys-label-medium);
        mat-icon {
          font-size: 18px;
          width: 18px;
          height: 18px;
        }
      }
      .done .dot {
        border-color: var(--mat-sys-primary);
        background: var(--mat-sys-primary);
        color: var(--mat-sys-on-primary);
      }
      .current .dot {
        border-color: var(--mat-sys-primary);
        color: var(--mat-sys-primary);
      }
      .current .label {
        color: var(--mat-sys-on-surface);
        font-weight: 500;
      }
      .label {
        font: var(--mat-sys-label-medium);
        text-align: center;
      }
    }
    .message {
      margin: 8px 0;
      &.positive {
        color: var(--mat-sys-primary);
        font-weight: 500;
      }
    }
    .interview {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .recruiter {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-top: 12px;
      padding-top: 12px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      .name {
        font-weight: 500;
      }
      a {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        color: var(--mat-sys-primary);
        mat-icon {
          font-size: 18px;
          width: 18px;
          height: 18px;
        }
      }
    }
  `,
})
export class ApplicationPage {
  readonly apps = inject(CandidatePortal).applications();
  readonly steps = STEPS;
  readonly message = statusMessage;

  indexOf(a: CandidateApplicationDto): number {
    return STEPS.findIndex((s) => s.step === a.step);
  }

  labelOf(a: CandidateApplicationDto): string {
    return STEPS[this.indexOf(a)]?.label ?? '';
  }
}
