import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { decimalString } from '@platform/shared';
import { zodValidator } from '@platform/ui';
import { z } from 'zod';
import type { EmployeeDetailDto, EmployeeOptionsDto } from '../data/api-types';
import { EmployeesRepository } from '../data/payroll-repositories';
import { PanelState } from '../clients/panel-base';

export interface AssignmentPanelData {
  /** assignment: «Cambiar cliente o puesto» · edit: turno, tarifas propias, comentarios. */
  readonly mode: 'assignment' | 'edit';
  readonly employee: EmployeeDetailDto;
  readonly options: EmployeeOptionsDto;
}

const hourly = z.string().trim().regex(/^\d{1,6}(\.\d{1,4})?$/, 'Importe con hasta 4 decimales.');

/**
 * «Cambiar cliente o puesto» takes effect now; the previous assignment stays in the audit trail (PLAN D82). «Editar» changes
 * the turno, the employee's own rates over the puesto's and the COMENTARIOS.
 */
@Component({
  selector: 'app-assignment-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatAutocomplete, MatAutocompleteTrigger, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.mode === 'assignment' ? 'Cambiar cliente o puesto' : 'Editar empleado' }}</h2>
    <mat-dialog-content>
      <p class="muted">{{ e.fullName }} · {{ e.clientName }} — {{ e.groupName }} · {{ e.positionName }}</p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="assignment-form" novalidate>
        @if (data.mode === 'assignment') {
          <mat-form-field>
            <mat-label>Grupo de nómina</mat-label>
            <mat-select formControlName="payrollGroupId">
              @for (g of data.options.groups; track g.id) {
                <mat-option [value]="g.id">{{ g.clientName }} — {{ g.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Puesto</mat-label>
            <mat-select formControlName="positionId">
              @for (p of positions(); track p.id) {
                <mat-option [value]="p.id">{{ p.name }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('positionId') }}</mat-error>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Turno</mat-label>
          <input matInput formControlName="shift" [matAutocomplete]="shifts" autocomplete="off" />
          <mat-autocomplete #shifts="matAutocomplete">
            @for (t of data.options.shifts; track t) {
              <mat-option [value]="t">{{ t }}</mat-option>
            }
          </mat-autocomplete>
          <mat-error>{{ s.msg('shift') }}</mat-error>
        </mat-form-field>
        @if (data.mode === 'edit') {
          <h3 class="section-title">Tarifas propias (vacío = las del puesto)</h3>
          <div class="row">
            <mat-form-field>
              <mat-label>SD bolsillo</mat-label>
              <input matInput formControlName="pocket" inputmode="decimal" autocomplete="off" />
              <mat-hint>Puesto: $ {{ e.positionRates?.dailySalaryPocket ?? '—' }}</mat-hint>
              <mat-error>{{ s.msg('pocket') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>SD IMSS</mat-label>
              <input matInput formControlName="imss" inputmode="decimal" autocomplete="off" />
              <mat-hint>Puesto: $ {{ e.positionRates?.dailySalaryImss ?? '—' }}</mat-hint>
              <mat-error>{{ s.msg('imss') }}</mat-error>
            </mat-form-field>
          </div>
          <div class="row">
            <mat-form-field>
              <mat-label>Costo por hora</mat-label>
              <input matInput formControlName="hourly" inputmode="decimal" autocomplete="off" />
              <mat-hint>Puesto: $ {{ e.positionRates?.hourlyRate ?? '—' }}</mat-hint>
              <mat-error>{{ s.msg('hourly') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Bono días trabajados</mat-label>
              <input matInput formControlName="bonus" inputmode="decimal" autocomplete="off" />
              <mat-hint>Puesto: $ {{ e.positionRates?.daysWorkedBonus ?? '—' }}</mat-hint>
              <mat-error>{{ s.msg('bonus') }}</mat-error>
            </mat-form-field>
          </div>
          <mat-form-field>
            <mat-label>Comentarios</mat-label>
            <textarea matInput formControlName="notes" rows="3"></textarea>
          </mat-form-field>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="assignment-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      mat-form-field {
        flex: 1 1 140px;
      }
    }
  `,
})
export class AssignmentPanel {
  readonly data = inject<AssignmentPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<AssignmentPanel>);
  private readonly employees = inject(EmployeesRepository);
  private readonly snack = inject(MatSnackBar);
  readonly e = this.data.employee;
  private readonly money = (v: unknown) => (v == null ? '' : String(v));

  readonly form = inject(FormBuilder).nonNullable.group({
    payrollGroupId: [this.e.payrollGroupId, Validators.required],
    positionId: [this.e.positionId as string | null, Validators.required],
    shift: [this.e.shift, [Validators.required, Validators.maxLength(30)]],
    pocket: [this.money(this.e.dailySalaryPocketOverride), zodValidator(decimalString())],
    imss: [this.money(this.e.dailySalaryImssOverride), zodValidator(decimalString())],
    hourly: [this.money(this.e.hourlyRateOverride), zodValidator(hourly)],
    bonus: [this.money(this.e.daysWorkedBonusOverride), zodValidator(decimalString())],
    notes: [this.e.notes ?? '', Validators.maxLength(500)],
  });
  readonly s = new PanelState(this.form);
  private readonly groupId = toSignal(this.form.controls.payrollGroupId.valueChanges, { initialValue: this.e.payrollGroupId });
  readonly positions = computed(() => this.data.options.groups.find((g) => g.id === this.groupId())?.positions ?? []);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      if (this.data.mode === 'assignment') await this.employees.changeAssignment(this.e.id, v.payrollGroupId, v.positionId!, v.shift);
      else
        await this.employees.update(this.e.id, {
          shift: v.shift,
          dailySalaryPocketOverride: text(v.pocket),
          dailySalaryImssOverride: text(v.imss),
          hourlyRateOverride: text(v.hourly),
          daysWorkedBonusOverride: text(v.bonus),
          notes: text(v.notes),
        });
      this.snack.open('Empleado actualizado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
