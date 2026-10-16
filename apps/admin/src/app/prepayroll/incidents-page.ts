import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOptgroup, MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { type DateKey, formatDateKey } from '@platform/shared';
import { confirm, openSidePanel } from '@platform/ui';
import type { IncidentDto, PayrollPeriodDto } from '../data/api-types';
import { INCIDENT_CONCEPTS, money, PERIOD_STATUS, periodLabel } from '../data/payroll-labels';
import { IncidentsRepository } from '../data/payroll-repositories';
import { IncidentPanel, type IncidentPanelData } from './incident-panel';

/**
 * Nómina → Incidencias (SPEC §5.4): what changes pay and is not attendance — horas extra, dobles y triples, retroactivos,
 * descuentos y abonos a préstamo — by period. One dated in a paid period is paid in the next open one («corresponde a la
 * semana 38»); one captured while the period is in revisión is marked so. Ajustes manuales are made on the Prenómina grid.
 */
@Component({
  selector: 'app-incidents-page',
  imports: [MatButton, MatIconButton, MatFormField, MatLabel, MatIcon, MatProgressBar, MatSelect, MatOption, MatOptgroup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Incidencias</h1>
        <button matButton="filled" type="button" (click)="edit(null)" [disabled]="!period() || period()!.status === 'paid'">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nueva incidencia
        </button>
      </header>
      <mat-form-field subscriptSizing="dynamic" class="period">
        <mat-label>Período</mat-label>
        <mat-select [value]="periodId()" (valueChange)="periodId.set($event)">
          @for (g of groups(); track g.key) {
            <mat-optgroup [label]="g.label">
              @for (p of g.periods; track p.id) {
                <mat-option [value]="p.id">{{ label(p) }} · {{ status[p.status!].label }}</mat-option>
              }
            </mat-optgroup>
          }
        </mat-select>
      </mat-form-field>
      @if (period()?.status === 'paid') {
        <p class="banner">Período pagado: una incidencia con fecha aquí se paga en el siguiente período abierto como retroactivo. Créala desde ese período.</p>
      }
      @if (list.loading() && !list.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="list">
        @for (i of rows(); track i.id) {
          <section class="row">
            <div class="who">
              <div class="name">{{ i.fullName }}</div>
              <div class="muted">
                {{ date(i.date) }} · {{ i.conceptName }}@if (i.adjustedColumn) { · {{ i.adjustedColumn }} }@if (i.reason) { · {{ i.reason }} }
              </div>
              <div class="tags">
                @if (i.periodId !== periodId()) { <span class="pill">Se paga en la semana {{ i.periodNumber }}</span> }
                @if (i.originPeriodNumber) { <span class="pill">Corresponde a la semana {{ i.originPeriodNumber }}</span> }
                @if (i.duringReview) { <span class="pill negative">Durante revisión</span> }
                <span class="muted">Registró {{ i.reportedBy }}</span>
              </div>
            </div>
            <div class="value">{{ value(i) }}</div>
            @if (i.editable) {
              @if (!i.adjustedColumn) {
                <button matIconButton type="button" (click)="edit(i)" [attr.aria-label]="'Editar incidencia de ' + i.fullName"><mat-icon fontSet="material-symbols-outlined">edit</mat-icon></button>
              }
              <button matIconButton type="button" (click)="remove(i)" [attr.aria-label]="'Quitar incidencia de ' + i.fullName"><mat-icon fontSet="material-symbols-outlined">delete</mat-icon></button>
            }
          </section>
        } @empty {
          @if (list.value()) {
            <p class="list-empty">Sin incidencias en este período.</p>
          }
        }
      </div>
    </div>
  `,
  styles: `
    .period {
      width: min(460px, 100%);
      margin-bottom: 12px;
    }
    .banner {
      padding: 10px 12px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
    }
    .list {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 8px 12px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      &:last-child {
        border-bottom: 0;
      }
    }
    .who {
      flex: 1 1 auto;
      min-width: 0;
      .name {
        font-weight: 500;
      }
    }
    .tags {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      margin-top: 4px;
    }
    .value {
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
      font-weight: 500;
    }
  `,
})
export class IncidentsPage {
  private readonly repo = inject(IncidentsRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  readonly status = PERIOD_STATUS;
  readonly periods = this.repo.periods();
  readonly periodId = signal<string | null>(this.params.get('period'));
  readonly list = this.repo.list(() => this.periodId());
  readonly period = computed(() => this.periods.value()?.find((p) => p.id === this.periodId()) ?? null);
  readonly groups = computed(() => {
    const map = new Map<string, { key: string; label: string; periods: PayrollPeriodDto[] }>();
    for (const p of this.periods.value() ?? []) {
      const g = map.get(p.groupId) ?? { key: p.groupId, label: `${p.clientName} · ${p.groupName}`, periods: [] };
      g.periods.push(p);
      map.set(p.groupId, g);
    }
    return [...map.values()];
  });
  /** Ajustes manuales are the grid's (its desglose lists and removes them). */
  readonly rows = computed(() => this.list.value() ?? []);

  constructor() {
    effect(() => {
      const periods = this.periods.value();
      if (!periods?.length || periods.some((p) => p.id === untracked(this.periodId))) return;
      const open = periods.filter((p) => p.status !== 'paid' && Number(p.lines) > 0);
      this.periodId.set((open.at(-1) ?? periods[0]).id);
    });
    let first = true;
    effect(() => {
      const period = this.periodId();
      void this.router.navigate([], { queryParams: { period }, replaceUrl: true });
      if (first) {
        first = false;
        return;
      }
      untracked(() => this.list.reload());
    });
  }

  label(p: PayrollPeriodDto): string {
    return periodLabel(p, p.weekly);
  }

  date(d: string): string {
    return formatDateKey(d as DateKey);
  }

  value(i: IncidentDto): string {
    const byHours = INCIDENT_CONCEPTS.find((c) => c.code === i.conceptCode)?.byHours;
    const sign = ['DEDUCTION', 'LOAN_INSTALLMENT'].includes(i.conceptCode) ? '-' : '';
    return byHours ? `${+i.quantity} h` : sign + money(i.amount);
  }

  edit(i: IncidentDto | null): void {
    const p = this.period();
    if (!p) return;
    openSidePanel<IncidentPanel, IncidentPanelData>(this.dialog, IncidentPanel, { period: p, incident: i });
  }

  async remove(i: IncidentDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: '¿Quitar la incidencia?',
      body: `${i.conceptName} de ${i.fullName}. Su línea de prenómina se recalcula sin ella.`,
      confirm: 'Quitar',
      destructive: true,
    });
    if (!ok) return;
    try {
      await this.repo.remove(i.id);
      this.snack.open('Incidencia quitada', undefined, { duration: 3000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo quitar.', 'Cerrar');
    }
  }
}
