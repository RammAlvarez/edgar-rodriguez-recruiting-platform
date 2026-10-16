import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { type DateKey, formatDateKey } from '@platform/shared';
import type { AttendanceEntryDto, AttendanceStatus } from '../data/api-types';
import { ATTENDANCE } from '../data/payroll-labels';
import { AttendanceRepository } from '../data/payroll-repositories';
import { PanelState } from '../clients/panel-base';
import { CAPTURE_STATUSES } from './attendance-labels';

export interface UnlockPanelData {
  readonly siteId: string;
  readonly date: DateKey;
  readonly entry: AttendanceEntryDto;
}

/**
 * «Desbloquear» (SPEC §5.3, PLAN D83): the new value of a closed day and why. The change and its reason are kept with the
 * values before and after; the day stays closed, so another change needs another reason.
 */
@Component({
  selector: 'app-unlock-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Cambiar día cerrado</h2>
    <mat-dialog-content>
      <p class="muted">
        {{ data.entry.fullName }} · {{ format(data.date) }}
        @if (data.entry.record; as r) { · ahora {{ label[r.status!].label }} } @else { · sin captura }
      </p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="unlock-form" novalidate>
        <mat-form-field>
          <mat-label>Asistencia</mat-label>
          <mat-select formControlName="status">
            @for (s of statuses; track s) {
              <mat-option [value]="s">{{ label[s].label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (worked()) {
          <div class="row">
            <mat-form-field>
              <mat-label>Horas</mat-label>
              <input matInput formControlName="hoursWorked" inputmode="decimal" autocomplete="off" />
              <mat-hint>{{ status() === 'partial' ? 'Menos de ' + data.entry.shiftHours : 'Vacío = ' + data.entry.shiftHours }}</mat-hint>
              <mat-error>{{ s.msg('hoursWorked') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Horas extra</mat-label>
              <input matInput formControlName="overtimeHours" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('overtimeHours') }}</mat-error>
            </mat-form-field>
          </div>
        }
        <mat-form-field>
          <mat-label>Motivo del cambio</mat-label>
          <textarea matInput formControlName="reason" rows="2"></textarea>
          <mat-hint>Queda registrado con quién y cuándo</mat-hint>
          <mat-error>{{ s.msg('reason') }}</mat-error>
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="unlock-form" [disabled]="s.busy()">Guardar cambio</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      mat-form-field {
        flex: 1 1 120px;
      }
    }
  `,
})
export class UnlockPanel {
  readonly data = inject<UnlockPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<UnlockPanel>);
  private readonly attendance = inject(AttendanceRepository);
  private readonly snack = inject(MatSnackBar);
  readonly label = ATTENDANCE;
  readonly statuses = CAPTURE_STATUSES;
  readonly format = formatDateKey;
  private readonly r = this.data.entry.record;

  readonly form = inject(FormBuilder).nonNullable.group({
    status: [(this.r?.status ?? 'present') as AttendanceStatus],
    hoursWorked: [this.r?.hoursWorked == null ? '' : String(Number(this.r.hoursWorked))],
    overtimeHours: [this.r && Number(this.r.overtimeHours) > 0 ? String(Number(this.r.overtimeHours)) : ''],
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });
  readonly s = new PanelState(this.form);
  readonly status = toSignal(this.form.controls.status.valueChanges, { initialValue: this.form.controls.status.value });
  readonly worked = () => ['present', 'partial', 'rest_day_worked', 'holiday_worked'].includes(this.status());

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      await this.attendance.unlock({
        employeeId: this.data.entry.employeeId,
        date: this.data.date,
        siteId: this.data.siteId,
        status: v.status,
        hoursWorked: this.worked() ? text(v.hoursWorked) : null,
        overtimeHours: this.worked() ? text(v.overtimeHours) : null,
        note: null,
        reason: v.reason.trim(),
      });
      this.snack.open('Día corregido; el cambio quedó registrado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
