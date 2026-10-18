import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatChipListbox, MatChipOption } from '@angular/material/chips';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { todayKey } from '@platform/shared';
import type { PayrollReport, PayrollReportRowDto, ReportFormat } from '../data/api-types';
import { type PayrollReportQuery, PayrollReportsRepository } from '../data/payroll-repositories';
import { DATE_MODE, monthEnd, monthStart, PAYROLL_REPORTS, payrollPreset, type PayrollPreset, PAYROLL_PRESETS } from './payroll-report-format';

/**
 * Nómina → Reportes de nómina (SPEC §5.9, PLAN D87): the selector on the left; Cliente and the date control the report takes —
 * Semana (default: the last paid period) or Rango for Costo, Incidencias and Asistencia, Semana for Resumen de pago, months for
 * Rotación, nothing for Tarjetas y cuentas —; the report's tables as the API builds them, and the export of exactly that.
 */
@Component({
  selector: 'app-payroll-reports-page',
  imports: [MatButton, MatChipListbox, MatChipOption, MatFormField, MatIcon, MatInput, MatLabel, MatOption, MatProgressBar, MatSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Reportes de nómina</h1></header>
      <div class="layout">
        <nav class="selector" aria-label="Reportes">
          @for (r of reports; track r.report) {
            <button type="button" [class.current]="r.report === report()" [attr.aria-current]="r.report === report() ? 'page' : null"
              (click)="report.set(r.report)">
              <mat-icon fontSet="material-symbols-outlined">{{ r.icon }}</mat-icon>
              <span>{{ r.label }}</span>
            </button>
          }
        </nav>
        <section class="body">
          <div class="filter-bar">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>Cliente</mat-label>
              <mat-select [value]="clientId()" (valueChange)="setClient($event)">
                <mat-option [value]="null">Todos</mat-option>
                @for (c of options.value()?.clients ?? []; track c.id) {
                  <mat-option [value]="c.id">{{ c.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            @switch (mode()) {
              @case ('week-or-range') {
                <mat-chip-listbox aria-label="Fechas" [value]="useRange() ? 'range' : 'week'" (change)="useRange.set($event.value === 'range')">
                  <mat-chip-option value="week">Semana</mat-chip-option>
                  <mat-chip-option value="range">Rango</mat-chip-option>
                </mat-chip-listbox>
              }
            }
            @if (showsPeriod()) {
              <mat-form-field subscriptSizing="dynamic" class="period">
                <mat-label>Semana</mat-label>
                <mat-select [value]="shownPeriod()" (valueChange)="periodId.set($event)">
                  @for (p of periods(); track p.id) {
                    <mat-option [value]="p.id">{{ p.label }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            @if (showsRange()) {
              @if (mode() === 'months') {
                <mat-form-field subscriptSizing="dynamic" class="date">
                  <mat-label>Desde (mes)</mat-label>
                  <input matInput type="month" [value]="monthFrom().slice(0, 7)" (change)="setMonth('from', $event)" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic" class="date">
                  <mat-label>Hasta (mes)</mat-label>
                  <input matInput type="month" [value]="monthTo().slice(0, 7)" (change)="setMonth('to', $event)" />
                </mat-form-field>
              } @else {
                <mat-chip-listbox aria-label="Rango" [value]="preset()" (change)="pick($event.value)">
                  @for (p of presets; track p.preset) {
                    <mat-chip-option [value]="p.preset">{{ p.label }}</mat-chip-option>
                  }
                </mat-chip-listbox>
                <mat-form-field subscriptSizing="dynamic" class="date">
                  <mat-label>Desde</mat-label>
                  <input matInput type="date" [value]="from()" [max]="to()" (change)="setDate('from', $event)" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic" class="date">
                  <mat-label>Hasta</mat-label>
                  <input matInput type="date" [value]="to()" [min]="from()" (change)="setDate('to', $event)" />
                </mat-form-field>
              }
            }
            <span class="actions">
              <button matButton="outlined" type="button" [disabled]="exporting() !== null || !shown()" (click)="export('xlsx')">
                <mat-icon fontSet="material-symbols-outlined">table_view</mat-icon>Exportar Excel
              </button>
              <button matButton="outlined" type="button" [disabled]="exporting() !== null || !shown()" (click)="export('pdf')">
                <mat-icon fontSet="material-symbols-outlined">picture_as_pdf</mat-icon>Exportar PDF
              </button>
            </span>
          </div>
          @if (shown(); as r) {
            <h2>{{ r.title }}<span class="muted"> · {{ r.filters.description }}</span></h2>
          }
          @if (result.loading() || exporting()) {
            <mat-progress-bar mode="indeterminate" />
          }
          @if (result.error(); as e) {
            <p class="form-alert" role="alert">{{ e.message }}</p>
          }
          @if (shown(); as r) {
            @for (t of r.tables; track $index) {
              @if (t.heading) {
                <h3>{{ t.heading }}</h3>
              }
              <div class="table-card scroll">
                <table>
                  <thead>
                    <tr>
                      @for (c of t.columns; track $index) {
                        <th [class.num]="numeric(t.rows, $index)">{{ c }}</th>
                      }
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of t.rows; track $index) {
                      <tr [class.total]="row.emphasis" [class.clickable]="r.report === 'cost' && row.periodId" (click)="open(r.report, row)">
                        @for (cell of row.cells; track $index) {
                          <td [class.num]="cell.kind !== 'text'" [class.letter]="cell.display.length <= 2 && cell.kind === 'text'" [class.long]="cell.display.length > 40">{{ cell.display }}</td>
                        }
                      </tr>
                    } @empty {
                      <tr><td [attr.colspan]="t.columns.length" class="list-empty">Sin registros.</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            }
            @if (r.note) {
              <p class="muted note">{{ r.note }}</p>
            }
          }
        </section>
      </div>
    </div>
  `,
  styles: `
    .layout {
      display: flex;
      gap: 24px;
      align-items: flex-start;
      @media (max-width: 959.98px) {
        flex-direction: column;
        gap: 16px;
      }
    }
    .selector {
      flex: 0 0 288px;
      box-sizing: border-box;
      padding: 8px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      @media (max-width: 959.98px) {
        flex-basis: auto;
        width: 100%;
      }
      button {
        all: unset;
        box-sizing: border-box;
        display: flex;
        align-items: center;
        gap: 12px;
        width: 100%;
        padding: 10px 12px;
        border-radius: 9999px;
        cursor: pointer;
        font: var(--mat-sys-label-large);
        color: var(--mat-sys-on-surface-variant);
        &:hover {
          background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent);
        }
        &:focus-visible {
          outline: 2px solid var(--mat-sys-primary);
        }
        &.current {
          background: var(--mat-sys-secondary-container);
          color: var(--mat-sys-on-secondary-container);
        }
      }
    }
    .body {
      flex: 1;
      min-width: 0;
      width: 100%;
    }
    .filter-bar {
      margin-bottom: 16px;
      .date {
        width: 160px;
      }
      .period {
        width: min(420px, 100%);
      }
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-left: auto;
    }
    h2 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-medium);
      .muted {
        font: var(--mat-sys-body-medium);
      }
    }
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }
    .scroll {
      overflow-x: auto;
    }
    table {
      border-collapse: collapse;
      font: var(--mat-sys-body-small);
      min-width: 100%;
    }
    th,
    td {
      padding: 6px 10px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      text-align: left;
      vertical-align: top;
      white-space: nowrap;
    }
    th {
      white-space: normal;
      min-width: 64px;
    }
    td.long {
      white-space: normal;
      min-width: 240px;
    }
    th {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .num {
      text-align: right;
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
    .letter {
      text-align: center;
    }
    tr.total td {
      background: var(--mat-sys-surface-container);
      font-weight: 500;
    }
    tr.clickable {
      cursor: pointer;
      &:hover td {
        background: color-mix(in srgb, var(--mat-sys-on-surface) 6%, transparent);
      }
    }
    .note {
      margin-top: 8px;
    }
  `,
})
export class PayrollReportsPage {
  private readonly repository = inject(PayrollReportsRepository);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly today = todayKey();

  readonly reports = PAYROLL_REPORTS;
  readonly presets = PAYROLL_PRESETS;
  readonly report = signal<PayrollReport>('cost');
  readonly clientId = signal<string | null>(null);
  /** The Semana chosen; null = the API's default (the last paid period), shown once the report answers. */
  readonly periodId = signal<string | null>(null);
  readonly useRange = signal(false);
  readonly preset = signal<PayrollPreset | null>('weeks');
  readonly from = signal<string>(payrollPreset('weeks', this.today).from);
  readonly to = signal<string>(this.today);
  /** Rotación's months: the year so far by default. */
  readonly monthFrom = signal<string>(`${this.today.slice(0, 4)}-01-01`);
  readonly monthTo = signal<string>(this.today);
  readonly exporting = signal<ReportFormat | null>(null);

  readonly mode = computed(() => DATE_MODE[this.report()]);
  readonly showsPeriod = computed(() => this.mode() === 'week' || (this.mode() === 'week-or-range' && !this.useRange()));
  readonly showsRange = computed(() => this.mode() === 'months' || (this.mode() === 'week-or-range' && this.useRange()));

  readonly options = this.repository.options();
  readonly periods = computed(() => {
    const client = this.clientId();
    return (this.options.value()?.periods ?? []).filter((p) => !client || p.clientId === client);
  });

  private readonly query = computed<PayrollReportQuery>(() => {
    const months = this.mode() === 'months';
    return {
      report: this.report(),
      clientId: this.clientId(),
      periodId: this.showsPeriod() ? this.periodId() : null,
      from: this.showsRange() ? (months ? monthStart(this.monthFrom()) : this.from()) : null,
      to: this.showsRange() ? (months ? monthEnd(this.monthTo()) : this.to()) : null,
    };
  });

  readonly result = this.repository.report(() => untracked(this.query));
  /** Only the selected report's body: while another loads, the previous tables are not shown under the new title. */
  readonly shown = computed(() => {
    const r = this.result.value();
    return r && r.report === this.report() ? r : undefined;
  });
  readonly shownPeriod = computed(() => this.periodId() ?? this.shown()?.filters.periodId ?? null);

  constructor() {
    let first = true;
    effect(() => {
      this.query();
      if (first) {
        first = false;
        return;
      }
      untracked(() => this.result.reload());
    });
  }

  numeric(rows: readonly PayrollReportRowDto[], index: number): boolean {
    return rows.some((r) => !r.emphasis && r.cells[index]?.kind !== 'text');
  }

  setClient(id: string | null): void {
    this.clientId.set(id);
    // A period of another client no longer applies.
    if (id && this.periodId() && !this.periods().some((p) => p.id === this.periodId())) this.periodId.set(null);
  }

  pick(preset: PayrollPreset | null | undefined): void {
    if (!preset) return;
    const range = payrollPreset(preset, this.today);
    this.preset.set(preset);
    this.from.set(range.from);
    this.to.set(range.to);
  }

  setDate(which: 'from' | 'to', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return;
    if (which === 'from' && value <= this.to()) this.from.set(value);
    else if (which === 'to' && value >= this.from()) this.to.set(value);
    else return;
    this.preset.set(null);
  }

  setMonth(which: 'from' | 'to', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (!/^\d{4}-\d{2}$/.test(value)) return;
    if (which === 'from' && `${value}-01` <= this.monthTo()) this.monthFrom.set(`${value}-01`);
    else if (which === 'to' && `${value}-01` >= monthStart(this.monthFrom())) this.monthTo.set(`${value}-01`);
  }

  open(report: PayrollReport, row: PayrollReportRowDto): void {
    if (report === 'cost' && row.periodId) void this.router.navigate(['/prenomina'], { queryParams: { period: row.periodId } });
  }

  async export(format: ReportFormat): Promise<void> {
    this.exporting.set(format);
    try {
      const file = await this.repository.export(this.query(), format);
      // The presigned GET answers «attachment», so the browser downloads it and stays on this page.
      window.location.assign(file.url);
      this.snackBar.open(`${file.fileName} generado`, undefined, { duration: 4000 });
    } catch (e) {
      this.snackBar.open(e instanceof ApiError ? e.message : 'No se pudo generar el archivo.', 'Cerrar');
    } finally {
      this.exporting.set(null);
    }
  }
}
