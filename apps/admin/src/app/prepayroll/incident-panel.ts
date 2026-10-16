import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PanelState } from '@platform/ui';
import type { IncidentDto, PayrollPeriodDto } from '../data/api-types';
import { INCIDENT_CONCEPTS, periodLabel } from '../data/payroll-labels';
import { IncidentsRepository } from '../data/payroll-repositories';

export interface IncidentPanelData {
  readonly period: PayrollPeriodDto;
  readonly incident: IncidentDto | null;
}

/**
 * «Nueva incidencia» / edit: who, what, when, and hours (horas extra) or an amount (the rest). The date decides the period it is
 * paid in; a date in a paid period goes to the next open one as a retroactivo (the list says so).
 */
@Component({
  selector: 'app-incident-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.incident ? 'Editar incidencia' : 'Nueva incidencia' }}</h2>
    <mat-dialog-content>
      <p class="muted">{{ data.period.clientName }} · {{ data.period.groupName }} · {{ label }}</p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="incident-form" novalidate>
        <mat-form-field>
          <mat-label>Empleado</mat-label>
          <mat-select formControlName="employeeId">
            @for (e of employees.value() ?? []; track e.employeeId) {
              <mat-option [value]="e.employeeId">{{ e.fullName }} · {{ e.positionName }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('employeeId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Tipo</mat-label>
          <mat-select formControlName="conceptCode">
            @for (c of concepts; track c.code) {
              <mat-option [value]="c.code">{{ c.label }}</mat-option>
            }
          </mat-select>
          <mat-hint>{{ concept()?.hint }}</mat-hint>
          <mat-error>{{ s.msg('conceptCode') }}</mat-error>
        </mat-form-field>
        <div class="row">
          <mat-form-field>
            <mat-label>Fecha</mat-label>
            <input matInput type="date" formControlName="date" />
            <mat-error>{{ s.msg('date') }}</mat-error>
          </mat-form-field>
          @if (concept()?.byHours) {
            <mat-form-field>
              <mat-label>Horas</mat-label>
              <input matInput formControlName="quantity" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('quantity') }}</mat-error>
            </mat-form-field>
          } @else {
            <mat-form-field>
              <mat-label>Importe</mat-label>
              <input matInput formControlName="amount" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('amount') }}</mat-error>
            </mat-form-field>
          }
        </div>
        <mat-form-field>
          <mat-label>Motivo</mat-label>
          <input matInput formControlName="reason" autocomplete="off" />
          <mat-error>{{ s.msg('reason') }}</mat-error>
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="incident-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      mat-form-field {
        flex: 1 1 140px;
      }
    }
  `,
})
export class IncidentPanel {
  readonly data = inject<IncidentPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<IncidentPanel>);
  private readonly repo = inject(IncidentsRepository);
  private readonly snack = inject(MatSnackBar);
  readonly concepts = INCIDENT_CONCEPTS;
  readonly label = periodLabel(this.data.period, this.data.period.weekly);
  readonly employees = this.repo.employees(() => this.data.period.id);
  private readonly i = this.data.incident;

  readonly form = inject(FormBuilder).nonNullable.group({
    employeeId: [this.i?.employeeId ?? '', Validators.required],
    conceptCode: [this.i?.conceptCode ?? 'OVERTIME_DOUBLE', Validators.required],
    date: [this.i?.date ?? (this.data.period.endDate as string), Validators.required],
    quantity: [this.i && +this.i.quantity > 0 ? String(+this.i.quantity) : ''],
    amount: [this.i && +this.i.amount > 0 ? String(+this.i.amount) : ''],
    reason: [this.i?.reason ?? '', Validators.maxLength(200)],
  });
  readonly s = new PanelState(this.form);
  private readonly code = toSignal(this.form.controls.conceptCode.valueChanges, { initialValue: this.form.controls.conceptCode.value });
  readonly concept = computed(() => INCIDENT_CONCEPTS.find((c) => c.code === this.code()));

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const byHours = !!this.concept()?.byHours;
      const clean = (x: string) => x.trim().replace(/[$,\s]/g, '') || null;
      const saved = await this.repo.save(this.i?.id ?? null, {
        employeeId: v.employeeId,
        conceptCode: v.conceptCode,
        date: v.date as never,
        quantity: byHours ? clean(v.quantity) : null,
        amount: byHours ? null : clean(v.amount),
        reason: v.reason.trim() || null,
      });
      this.snack.open(
        saved.originPeriodNumber ? `Guardada: se paga en la semana ${saved.periodNumber} como retroactivo` : 'Incidencia guardada',
        undefined,
        { duration: 4000 },
      );
      this.ref.close(true);
    });
  }
}
