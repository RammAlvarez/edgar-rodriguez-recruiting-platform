import { ChangeDetectionStrategy, Component, inject, Injector, input, type OnInit, runInInjectionContext, signal } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTooltip } from '@angular/material/tooltip';
import { type LiveQuery, openSidePanel } from '@platform/ui';
import { DateKeyPipe, MoneyPipe } from '@platform/ui/pipes';
import type { PayrollGroupDetailDto, PayrollGroupListItemDto, PositionDto } from '../data/api-types';
import { PERIOD_TYPE, WEEKDAYS_FROM_SUNDAY } from '../data/payroll-labels';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { GroupConceptsPanel, type GroupConceptsPanelData } from './group-concepts-panel';
import { PayrollGroupPanel, type PayrollGroupPanelData } from './payroll-group-panel';
import { PositionPanel, type PositionPanelData } from './position-panel';

/**
 * One grupo de nómina in Clientes (nómina) → client detail: its rules in one line, its puestos with the rates in force
 * (history on demand) and its bonuses. Rates live only here (SPEC §5.6).
 */
@Component({
  selector: 'app-payroll-group-card',
  imports: [MatButton, MatIconButton, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatProgressBar, MatTooltip, DateKeyPipe, MoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <header>
        <div class="grow">
          <h2>
            {{ summary().name }}
            @if (!summary().isActive) {
              <span class="pill">Inactivo</span>
            }
          </h2>
          <div class="muted">
            {{ period(summary()) }} · corte {{ cutoff(summary()) }} · {{ summary().shiftHours }} h por turno · cierre de asistencia
            {{ summary().attendanceLockTime.slice(0, 5) }} · {{ summary().activeEmployees }} empleados
          </div>
        </div>
        <button matButton type="button" (click)="editGroup()">
          <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>Editar
        </button>
      </header>
      @if (detail.loading() && !detail.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (detail.value(); as d) {
        <div class="table-card">
          <table>
            <thead>
              <tr>
                <th>Puesto</th><th class="num">SD bolsillo</th><th class="num">SD IMSS</th><th class="num">Costo por hora</th>
                <th class="num">Bono días trab.</th><th>Vigentes desde</th><th></th>
              </tr>
            </thead>
            <tbody>
              @for (p of d.positions; track p.id) {
                <tr [class.inactive]="!p.isActive">
                  <td>{{ p.name }}@if (!p.isActive) { <span class="muted"> · inactivo</span> }</td>
                  @if (p.current; as c) {
                    <td class="num">{{ c.dailySalaryPocket | money }}</td>
                    <td class="num">{{ c.dailySalaryImss | money }}</td>
                    <td class="num">$ {{ c.hourlyRate }}</td>
                    <td class="num">{{ c.daysWorkedBonus | money }}</td>
                    <td>{{ c.validFrom | dateKey }}</td>
                  } @else {
                    <td colspan="5" class="muted">Sin tarifas vigentes hoy@if (p.history[0]; as h) { · desde {{ h.validFrom | dateKey }} }</td>
                  }
                  <td class="actions">
                    <button matIconButton type="button" [matMenuTriggerFor]="menu" aria-label="Acciones del puesto">
                      <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
                    </button>
                    <mat-menu #menu="matMenu">
                      <button mat-menu-item type="button" (click)="position('rates', p)">Cambiar tarifas</button>
                      <button mat-menu-item type="button" (click)="toggleHistory(p.id)">{{ history() === p.id ? 'Ocultar historial' : 'Ver historial' }}</button>
                      <button mat-menu-item type="button" (click)="position('edit', p)">Editar puesto</button>
                    </mat-menu>
                  </td>
                </tr>
                @if (history() === p.id) {
                  @for (h of p.history; track h.id) {
                    <tr class="history">
                      <td>
                        <span class="muted">{{ h.validFrom | dateKey }} – {{ h.validTo ? (h.validTo | dateKey) : 'hoy' }}</span>
                      </td>
                      <td class="num">{{ h.dailySalaryPocket | money }}</td>
                      <td class="num">{{ h.dailySalaryImss | money }}</td>
                      <td class="num">$ {{ h.hourlyRate }}</td>
                      <td class="num">{{ h.daysWorkedBonus | money }}</td>
                      <td colspan="2"></td>
                    </tr>
                  }
                }
              } @empty {
                <tr class="empty-row"><td colspan="7">Sin puestos. Los puestos definen las tarifas que usa la prenómina.</td></tr>
              }
            </tbody>
          </table>
        </div>
        <div class="footer">
          <button matButton type="button" (click)="position('new', null)">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nuevo puesto
          </button>
          <span class="concepts">
            @for (c of d.concepts; track c.code) {
              <span class="pill" [class.positive]="c.isActive" [matTooltip]="c.isActive ? 'Se paga en este grupo' : 'Apagado'">
                {{ c.name }}@if (c.isActive && c.amount) { · {{ c.amount | money }}@if (c.daysBasis) { / {{ c.daysBasis }} d } }
              </span>
            }
            <button matButton type="button" (click)="concepts()">Bonos y conceptos</button>
          </span>
        </div>
      }
    </section>
  `,
  styles: `
    .card {
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      margin-bottom: 16px;
    }
    header {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      margin-bottom: 12px;
    }
    .grow {
      flex: 1;
      min-width: 0;
    }
    h2 {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      font: var(--mat-sys-title-medium);
    }
    table {
      border-collapse: collapse;
    }
    th,
    td {
      padding: 8px 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      text-align: left;
    }
    th {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    td.actions {
      width: 48px;
      padding: 0 4px;
    }
    tr.history td {
      background: var(--mat-sys-surface-container-low);
      font: var(--mat-sys-body-small);
    }
    tr.inactive td {
      color: var(--mat-sys-on-surface-variant);
    }
    .footer {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      margin-top: 8px;
    }
    .concepts {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      margin-left: auto;
    }
  `,
})
export class PayrollGroupCard implements OnInit {
  readonly summary = input.required<PayrollGroupListItemDto>();
  private readonly repository = inject(PayrollGroupsRepository);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);
  /** Started in ngOnInit: a live query runs at once and the required input is not bound in the field initializers. */
  detail!: LiveQuery<PayrollGroupDetailDto>;
  readonly history = signal<string | null>(null);

  ngOnInit(): void {
    this.detail = runInInjectionContext(this.injector, () => this.repository.detail(() => this.summary().id));
  }

  period(g: PayrollGroupListItemDto): string {
    return PERIOD_TYPE[g.periodType];
  }

  cutoff(g: PayrollGroupListItemDto): string {
    return WEEKDAYS_FROM_SUNDAY[Number(g.cutoffWeekday)].toLowerCase();
  }

  toggleHistory(id: string): void {
    this.history.update((h) => (h === id ? null : id));
  }

  async editGroup(): Promise<void> {
    const group = this.detail.value() ?? (await this.repository.get(this.summary().id));
    openSidePanel<PayrollGroupPanel, PayrollGroupPanelData>(this.dialog, PayrollGroupPanel, { clientId: group.clientId, group });
  }

  position(mode: PositionPanelData['mode'], position: PositionDto | null): void {
    openSidePanel<PositionPanel, PositionPanelData>(this.dialog, PositionPanel, {
      groupId: this.summary().id,
      groupName: this.summary().name,
      mode,
      position,
    });
  }

  concepts(): void {
    const d = this.detail.value();
    if (!d) return;
    openSidePanel<GroupConceptsPanel, GroupConceptsPanelData>(this.dialog, GroupConceptsPanel, {
      groupId: d.id,
      groupName: d.name,
      concepts: d.concepts,
    });
  }
}
