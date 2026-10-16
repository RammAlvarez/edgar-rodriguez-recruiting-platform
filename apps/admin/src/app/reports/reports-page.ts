import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatChipListbox, MatChipOption } from '@angular/material/chips';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import { type DateKey, formatDateKey, isDateKey, todayKey } from '@platform/shared';
import type { RecruitingReport, ReportFormat } from '../data/api-types';
import { ReportRepository, type ReportQuery } from '../data/report-repository';
import {
  ClientOpeningsBody,
  OpenOpeningsBody,
  RecruiterPerformanceBody,
  SourcesBody,
  StageFunnelBody,
  SummaryBody,
} from './report-bodies';
import { PRESETS, presetRange, type RangePreset, REPORTS } from './report-format';

/**
 * Reclutamiento → Reportes (SPEC §4.7, prototype «Reportes de reclutamiento»): the selector on the left, the filters on top —
 * Cliente, Reclutador and the range with presets (default *Año en curso*; report 3 is a snapshot with no range) — and the
 * export of exactly what is shown to Excel or PDF.
 */
@Component({
  selector: 'app-reports-page',
  imports: [
    MatButton,
    MatChipListbox,
    MatChipOption,
    MatFormField,
    MatIcon,
    MatInput,
    MatLabel,
    MatOption,
    MatProgressBar,
    MatSelect,
    RecruiterPerformanceBody,
    SummaryBody,
    OpenOpeningsBody,
    StageFunnelBody,
    SourcesBody,
    ClientOpeningsBody,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Reportes de reclutamiento</h1></header>
      <div class="layout">
        <nav class="selector" aria-label="Reportes">
          @for (r of reports; track r.report) {
            <button type="button" [class.current]="r.report === report()" [attr.aria-current]="r.report === report() ? 'page' : null"
              (click)="report.set(r.report)">
              <mat-icon fontSet="material-symbols-outlined" [class.filled]="r.report === report()">{{ r.icon }}</mat-icon>
              <span>{{ r.label }}</span>
            </button>
          }
        </nav>
        <section class="body">
          <div class="filter-bar">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>Cliente</mat-label>
              <mat-select [value]="clientId()" (valueChange)="clientId.set($event)">
                <mat-option [value]="null">Todos</mat-option>
                @for (c of options.value()?.clients ?? []; track c.id) {
                  <mat-option [value]="c.id">{{ c.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>Reclutador</mat-label>
              <mat-select [value]="recruiterId()" (valueChange)="recruiterId.set($event)">
                <mat-option [value]="null">Todos</mat-option>
                @for (r of options.value()?.recruiters ?? []; track r.id) {
                  <mat-option [value]="r.id">{{ r.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            @if (hasRange()) {
              <mat-chip-listbox aria-label="Rango" [value]="preset()" (change)="pick($event.value)" [hideSingleSelectionIndicator]="false">
                @for (p of presets; track p.preset) {
                  <mat-chip-option [value]="p.preset">{{ p.label }}</mat-chip-option>
                }
              </mat-chip-listbox>
              <mat-form-field subscriptSizing="dynamic" class="date">
                <mat-label>Desde</mat-label>
                <input matInput type="date" [value]="from()" [max]="to()" (change)="setFrom($event)" />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic" class="date">
                <mat-label>Hasta</mat-label>
                <input matInput type="date" [value]="to()" [min]="from()" (change)="setTo($event)" />
              </mat-form-field>
            }
            <span class="actions">
              <button matButton="outlined" type="button" [disabled]="exporting() !== null" (click)="export('xlsx')">
                <mat-icon fontSet="material-symbols-outlined">table_view</mat-icon>Exportar Excel
              </button>
              <button matButton="outlined" type="button" [disabled]="exporting() !== null" (click)="export('pdf')">
                <mat-icon fontSet="material-symbols-outlined">picture_as_pdf</mat-icon>Exportar PDF
              </button>
            </span>
          </div>
          <h2>{{ title() }}<span class="muted"> · {{ subtitle() }}</span></h2>
          @if (result.loading() || exporting()) {
            <mat-progress-bar mode="indeterminate" />
          }
          @if (result.error(); as e) {
            <p class="form-alert" role="alert">{{ e.message }}</p>
          }
          @if (shown(); as r) {
            @switch (r.report) {
              @case ('recruiter_performance') { <app-report-recruiter-performance [data]="r.data" /> }
              @case ('summary') { <app-report-summary [data]="r.data" /> }
              @case ('open_openings') { <app-report-open-openings [data]="r.data" /> }
              @case ('stage_funnel') { <app-report-stage-funnel [data]="r.data" /> }
              @case ('sources') { <app-report-sources [data]="r.data" /> }
              @case ('client_openings') { <app-report-client-openings [data]="r.data" /> }
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
      flex: 0 0 280px;
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
  `,
})
export class ReportsPage {
  private readonly repository = inject(ReportRepository);
  private readonly snackBar = inject(MatSnackBar);
  private readonly today = todayKey();

  readonly reports = REPORTS;
  readonly presets = PRESETS;
  readonly report = signal<RecruitingReport>('recruiter_performance');
  readonly clientId = signal<string | null>(null);
  readonly recruiterId = signal<string | null>(null);
  readonly preset = signal<RangePreset | null>('year');
  readonly from = signal<DateKey>(presetRange('year', this.today).from);
  readonly to = signal<DateKey>(this.today);
  readonly exporting = signal<ReportFormat | null>(null);

  readonly hasRange = computed(() => this.report() !== 'open_openings');
  readonly title = computed(() => REPORTS.find((r) => r.report === this.report())!.label);
  readonly subtitle = computed(() =>
    this.hasRange() ? `${formatDateKey(this.from())} – ${formatDateKey(this.to())}` : `situación al ${formatDateKey(this.today)}`,
  );

  private readonly query = computed<ReportQuery>(() => ({
    report: this.report(),
    clientId: this.clientId(),
    recruiterId: this.recruiterId(),
    from: this.from(),
    to: this.to(),
  }));

  readonly options = this.repository.options();
  readonly result = this.repository.report(() => untracked(this.query));
  /** Only the body of the selected report: while another one loads, the previous table is not shown under the new title. */
  readonly shown = computed(() => {
    const r = this.result.value();
    return r && r.report === this.report() ? r : undefined;
  });

  constructor() {
    // A filter change re-runs the report (the first run is the live query's own).
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

  pick(preset: RangePreset | null | undefined): void {
    if (!preset) return;
    const range = presetRange(preset, this.today);
    this.preset.set(preset);
    this.from.set(range.from);
    this.to.set(range.to);
  }

  setFrom(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (isDateKey(value) && value <= this.to()) {
      this.preset.set(null);
      this.from.set(value);
    }
  }

  setTo(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (isDateKey(value) && value >= this.from()) {
      this.preset.set(null);
      this.to.set(value);
    }
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
