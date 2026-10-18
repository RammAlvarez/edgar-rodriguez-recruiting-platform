import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { todayKey } from '@platform/shared';
import { Session } from '@platform/ui';
import { DateKeyPipe } from '@platform/ui/pipes';
import { PERIOD_STATUS, periodLabel } from '../data/payroll-labels';
import { AttendanceRepository, EmployeesRepository, PrepayrollRepository } from '../data/payroll-repositories';
import { CandidatesRepository, DashboardRepository, OpeningsRepository } from '../data/recruiting-repositories';

const OPEN = ['open', 'in_progress'];
/** Etapas where the candidate is still moving: not hired, not suitable, not discarded. */
const IN_PROCESS = ['new', 'contacted', 'interview', 'suitable'];

/**
 * Inicio (prototype «Inicio»): the recruiting counts from the vacantes and candidatos lists, the payroll ones for whoever holds
 * those grants, vacantes with the most posiciones por cubrir and the documents about to expire (next 30 days).
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, MatIcon, MatProgressBar, DateKeyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Inicio</h1></header>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (error(); as e) {
        <p class="form-alert" role="alert">{{ e }}</p>
      }
      <div class="kpis">
        <div class="kpi">
          <div class="label">Vacantes abiertas</div>
          <div class="value">{{ openOpenings().length }}</div>
          <div class="sub">{{ positionsLeft() }} posiciones por cubrir</div>
        </div>
        <div class="kpi">
          <div class="label">Candidatos en proceso</div>
          <div class="value">{{ inProcess() }}</div>
          <div class="sub">{{ (candidates.value() ?? []).length }} en total</div>
        </div>
        <div class="kpi">
          <div class="label">Contratados del mes</div>
          <div class="value">{{ hiredThisMonth() }}</div>
          <div class="sub">{{ monthLabel }}</div>
        </div>
        @if (employees) {
          <div class="kpi">
            <div class="label">Empleados activos</div>
            <div class="value">{{ activeEmployees() }}</div>
            <div class="sub">{{ onPayroll() }} en nómina</div>
          </div>
        }
        @if (sites) {
          <div class="kpi">
            <div class="label">Asistencia de hoy</div>
            <div class="value">{{ attendancePercent() }} %</div>
            <div class="sub">{{ captured() }} de {{ expected() }} capturados</div>
          </div>
        }
        @if (periods) {
          <div class="kpi">
            <div class="label">Prenómina en curso</div>
            @if (currentPeriod(); as p) {
              <div class="value text">{{ periodStatus[p.status].label }}</div>
              <div class="sub">{{ p.groupName }} · {{ periodText(p) }}</div>
            } @else {
              <div class="value text">—</div>
              <div class="sub">Sin periodos abiertos</div>
            }
          </div>
        }
      </div>

      <div class="lists">
        <section class="list-card">
          <h2>Vacantes por cubrir</h2>
          @for (o of toFill(); track o.id) {
            <a class="row" [routerLink]="['/vacantes', o.id]">
              <span class="grow">{{ o.displayName }}</span>
              <span class="count">{{ left(o) }} por cubrir</span>
            </a>
          } @empty {
            <p class="list-empty">No hay vacantes abiertas por cubrir.</p>
          }
        </section>
        <section class="list-card">
          <h2>Documentos por vencer</h2>
          @for (d of expiring.value() ?? []; track d.documentId) {
            <div class="row">
              <mat-icon class="warn" fontSet="material-symbols-outlined">schedule</mat-icon>
              <span class="grow">{{ d.fullName }} · {{ d.documentTypeName }}</span>
              <span class="muted date">{{ d.expiresAt | dateKey }}</span>
            </div>
          } @empty {
            <p class="list-empty">Ningún documento vence en los próximos 30 días.</p>
          }
        </section>
      </div>
    </div>
  `,
  styles: `
    .kpis {
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .kpi {
      padding: 16px;
      .label {
        font: var(--mat-sys-body-medium);
      }
      .value {
        margin: 4px 0;
        font: var(--mat-sys-headline-medium);
        &.text {
          font: var(--mat-sys-headline-small);
        }
      }
      .sub {
        font: var(--mat-sys-label-medium);
        color: var(--mat-sys-on-surface-variant);
      }
    }
    .lists {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(420px, 100%), 1fr));
      gap: 24px;
    }
    .list-card {
      padding: 16px 0 8px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      h2 {
        margin: 0 0 8px;
        padding: 0 16px;
        font: var(--mat-sys-title-medium);
      }
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 40px;
      padding: 4px 16px;
      color: inherit;
      text-decoration: none;
    }
    a.row:hover {
      background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent);
    }
    .grow {
      flex: 1;
      min-width: 0;
    }
    .count {
      color: var(--mat-sys-primary);
      font: var(--mat-sys-label-large);
    }
    .date {
      font-variant-numeric: tabular-nums;
    }
    .warn {
      color: var(--mat-sys-error);
    }
  `,
})
export class DashboardPage {
  private readonly session = inject(Session);
  readonly openings = inject(OpeningsRepository).list();
  readonly candidates = inject(CandidatesRepository).list();
  readonly expiring = inject(DashboardRepository).expiringDocuments();

  /** Payroll tiles only for Administradores holding those grants (the API refuses the rest). */
  readonly employees = this.session.hasGrant('payroll_staff') ? inject(EmployeesRepository).list(() => ({ includeTerminated: false })) : null;
  private readonly today = todayKey();
  readonly sites = this.session.hasGrant('payroll_attendance') ? inject(AttendanceRepository).sites(() => this.today) : null;
  readonly periods = this.session.hasGrant('payroll_prepayroll') ? inject(PrepayrollRepository).periods() : null;

  readonly periodStatus = PERIOD_STATUS;
  readonly monthLabel = new Date(`${this.today}T12:00:00Z`).toLocaleDateString('es-MX', { month: 'long', year: 'numeric', timeZone: 'UTC' });

  readonly loading = computed(() => this.openings.loading() && !this.openings.value());
  readonly error = computed(() => this.openings.error()?.message ?? this.candidates.error()?.message ?? null);

  readonly openOpenings = computed(() => (this.openings.value() ?? []).filter((o) => OPEN.includes(o.status)));
  readonly positionsLeft = computed(() => this.openOpenings().reduce((n, o) => n + this.left(o), 0));
  readonly inProcess = computed(() => (this.candidates.value() ?? []).filter((c) => IN_PROCESS.includes(c.stage)).length);
  readonly hiredThisMonth = computed(() => (this.candidates.value() ?? []).filter((c) => c.hiredAt?.startsWith(this.today.slice(0, 7))).length);
  readonly toFill = computed(() =>
    this.openOpenings()
      .filter((o) => this.left(o) > 0)
      .sort((a, b) => this.left(b) - this.left(a))
      .slice(0, 6),
  );

  readonly activeEmployees = computed(() => (this.employees?.value() ?? []).filter((e) => e.status === 'active').length);
  readonly onPayroll = computed(() => (this.employees?.value() ?? []).length);
  readonly expected = computed(() => (this.sites?.value() ?? []).reduce((n, s) => n + Number(s.expected), 0));
  readonly captured = computed(() => (this.sites?.value() ?? []).reduce((n, s) => n + Number(s.captured), 0));
  /** Whole percent, truncated like every percentage of the platform (PLAN D42). */
  readonly attendancePercent = computed(() => (this.expected() === 0 ? 0 : Math.trunc((this.captured() * 100) / this.expected())));
  /** The period being captured; when none, the one in review. */
  readonly currentPeriod = computed(() => {
    const all = this.periods?.value() ?? [];
    return all.find((p) => p.status === 'capturing') ?? all.find((p) => p.status === 'in_review') ?? null;
  });

  left(o: { headcountRequested: number | string; hired: number | string }): number {
    return Math.max(0, Number(o.headcountRequested) - Number(o.hired));
  }

  periodText(p: Parameters<typeof periodLabel>[0] & { weekly: boolean }): string {
    return periodLabel(p, p.weekly);
  }
}
