import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import { type DateKey, formatDateKey } from '@platform/shared';
import { confirm, PanelState } from '@platform/ui';
import type { IncidentDto } from '../data/api-types';
import { ATTENDANCE, money } from '../data/payroll-labels';
import { IncidentsRepository, PrepayrollRepository } from '../data/payroll-repositories';

export interface LinePanelData {
  readonly lineId: string;
  readonly columns: readonly string[];
  /** The cell tapped in the grid: the adjustment form opens on it. */
  readonly column: string | null;
  readonly current: string | null;
  readonly editable: boolean;
}

const SOURCE = { attendance: 'Asistencia', incident: 'Incidencia', position: 'Puesto', manual: 'Ajuste manual' } as const;

/**
 * «Desglose» of a prenómina row (SPEC §5.5): every item with its exact amount (6 decimals, D42) and where it comes from, the
 * days behind it, its incidencias and ajustes (removable while the period is open), the comments, and «Ajustar celda»: the new
 * value of a column, saved as an ajuste manual of the difference with its reason (D63).
 */
@Component({
  selector: 'app-line-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButton, MatIconButton, MatIcon, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (line.value(); as l) {
      <h2 mat-dialog-title>{{ l.fullName }}</h2>
    } @else {
      <h2 mat-dialog-title>Desglose</h2>
    }
    <mat-dialog-content>
      @if (line.loading() && !line.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (line.value(); as l) {
        <p class="muted">{{ l.positionName }} · {{ l.shift }} · {{ +l.daysWorked }} días · SD {{ money(l.dailySalaryPocket) }} · {{ +l.hourlyRate }} por hora</p>

        @if (data.editable) {
          <form class="panel-form adjust" [formGroup]="form" (ngSubmit)="adjust()" id="adjust-form" novalidate>
            <h3>Ajustar celda</h3>
            <div class="row">
              <mat-form-field>
                <mat-label>Columna</mat-label>
                <mat-select formControlName="column">
                  @for (c of data.columns; track c) {
                    <mat-option [value]="c">{{ c }}</mat-option>
                  }
                </mat-select>
                <mat-error>{{ s.msg('column') }}</mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Nuevo importe</mat-label>
                <input matInput formControlName="amount" inputmode="decimal" autocomplete="off" />
                <mat-hint>Como se ve; un descuento es negativo</mat-hint>
                <mat-error>{{ s.msg('amount') }}</mat-error>
              </mat-form-field>
            </div>
            <mat-form-field>
              <mat-label>Motivo</mat-label>
              <input matInput formControlName="reason" autocomplete="off" />
              <mat-error>{{ s.msg('reason') }}</mat-error>
            </mat-form-field>
            @if (s.alert()) {
              <p class="form-alert" role="alert">{{ s.alert() }}</p>
            }
            <div class="end"><button matButton="filled" type="submit" [disabled]="s.busy()">Guardar ajuste</button></div>
          </form>
        }

        <h3>Conceptos</h3>
        <table class="items">
          <tbody>
            @for (i of l.items; track $index) {
              <tr>
                <td>
                  {{ i.name }}@if (i.source === 'manual') { · {{ i.column }} }
                  <div class="muted">@if (i.source === 'manual') { {{ i.reason }} } @else { {{ source[i.source!] }} · {{ +i.quantity }} × {{ +i.unitValue }}@if (i.reason) { · {{ i.reason }} } }</div>
                </td>
                <td class="num">
                  {{ i.kind === 'deduction' ? '-' : '' }}{{ money(i.amount) }}
                  <div class="muted exact">{{ i.amount }}</div>
                </td>
              </tr>
            }
            <tr class="total">
              <td>TOTAL NETO A PAGAR<div class="muted exact">exacto {{ l.totalNetExact }}</div></td>
              <td class="num">{{ money(l.totalNet) }}</td>
            </tr>
          </tbody>
        </table>

        @if (l.incidents.length) {
          <h3>Incidencias y ajustes</h3>
          @for (i of l.incidents; track i.id) {
            <div class="incident">
              <div>
                <div>{{ i.conceptName }}@if (i.adjustedColumn) { · {{ i.adjustedColumn }} } · {{ incidentValue(i) }}</div>
                <div class="muted">
                  {{ date(i.date) }} · {{ i.reportedBy }}@if (i.reason) { · {{ i.reason }} }@if (i.duringReview) { · durante revisión }@if (i.originPeriodNumber) { · corresponde a la semana {{ i.originPeriodNumber }} }
                </div>
              </div>
              @if (i.editable && data.editable) {
                <button matIconButton type="button" (click)="remove(i)" [attr.aria-label]="'Quitar ' + i.conceptName">
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              }
            </div>
          }
        }

        <h3>Días</h3>
        <div class="days">
          @for (d of l.days; track d.date) {
            <span class="pill" [class]="label[d.status!].tone" [title]="d.isAutoAbsent ? 'Sin captura al cerrar' : ''">
              {{ day(d.date) }} · {{ label[d.status!].label }}@if (+(d.overtimeHours ?? 0) > 0) { +{{ +d.overtimeHours }} h }@if (d.isHoliday) { 🎉 }
            </span>
          } @empty {
            <span class="muted">Sin asistencia en el período.</span>
          }
        </div>

        <h3>Comentarios</h3>
        <mat-form-field class="full">
          <mat-label>Comentarios</mat-label>
          <textarea matInput rows="2" [value]="l.comments ?? ''" #comments [disabled]="!data.editable"></textarea>
          <mat-hint>Sin datos bancarios: la cuenta va en Personal</mat-hint>
        </mat-form-field>
        @if (data.editable) {
          <div class="end"><button matButton type="button" (click)="saveComments(comments.value)" [disabled]="savingComments()">Guardar comentarios</button></div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }
    .row {
      display: flex;
      gap: 12px;
      mat-form-field {
        flex: 1 1 160px;
      }
    }
    .adjust {
      padding: 4px 12px 8px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
    }
    .end {
      display: flex;
      justify-content: flex-end;
    }
    .full {
      width: 100%;
    }
    .items {
      width: 100%;
      border-collapse: collapse;
      td {
        padding: 6px 0;
        border-bottom: 1px solid var(--mat-sys-outline-variant);
        vertical-align: top;
      }
      .num {
        text-align: right;
        font-variant-numeric: tabular-nums;
        white-space: nowrap;
      }
      .exact {
        font-size: 11px;
      }
      .total td {
        font-weight: 600;
        border-bottom: 0;
      }
    }
    .incident {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .days {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
  `,
})
export class LinePanel {
  readonly data = inject<LinePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<LinePanel>);
  private readonly repo = inject(PrepayrollRepository);
  private readonly incidents = inject(IncidentsRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly label = ATTENDANCE;
  readonly source = SOURCE;
  readonly money = money;
  readonly line = this.repo.line(() => this.data.lineId);
  readonly savingComments = signal(false);

  readonly form = inject(FormBuilder).nonNullable.group({
    column: [this.data.column ?? this.data.columns[0] ?? '', Validators.required],
    amount: [this.data.current == null ? '' : String(Number(this.data.current)), Validators.required],
    reason: ['', [Validators.required, Validators.maxLength(200)]],
  });
  readonly s = new PanelState(this.form);

  date(d: string): string {
    return formatDateKey(d as DateKey);
  }

  day(d: string): string {
    return new Date(`${d}T12:00:00Z`).toLocaleDateString('es-MX', { weekday: 'short', day: 'numeric', timeZone: 'UTC' });
  }

  incidentValue(i: IncidentDto): string {
    return ['OVERTIME', 'OVERTIME_DOUBLE', 'OVERTIME_TRIPLE'].includes(i.conceptCode) ? `${+i.quantity} h` : money(i.amount);
  }

  adjust(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      await this.repo.adjust(this.data.lineId, v.column, v.amount.trim().replace(/[$,\s]/g, ''), v.reason.trim());
      this.snack.open(`Ajuste guardado en ${v.column}`, undefined, { duration: 4000 });
    });
  }

  async remove(i: IncidentDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: `¿Quitar ${i.adjustedColumn ? 'el ajuste' : 'la incidencia'}?`,
      body: 'La línea se recalcula sin ella.',
      confirm: 'Quitar',
      destructive: true,
    });
    if (!ok) return;
    try {
      await this.incidents.remove(i.id);
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo quitar.', 'Cerrar');
    }
  }

  async saveComments(value: string): Promise<void> {
    this.savingComments.set(true);
    try {
      await this.repo.setComments(this.data.lineId, value.trim() || null);
      this.snack.open('Comentarios guardados', undefined, { duration: 3000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? (e.fieldErrors ? Object.values(e.fieldErrors)[0]?.[0] ?? e.message : e.message) : 'No se pudo guardar.', 'Cerrar');
    } finally {
      this.savingComments.set(false);
    }
  }
}
