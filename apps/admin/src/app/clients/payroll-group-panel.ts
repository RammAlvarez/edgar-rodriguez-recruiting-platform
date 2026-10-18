import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { zodValidator } from '@platform/ui';
import { z } from 'zod';
import type { PayrollGroupDetailDto, PeriodType } from '../data/api-types';
import { PERIOD_TYPE, WEEKDAYS_FROM_SUNDAY } from '../data/payroll-labels';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { PanelState } from './panel-base';

export interface PayrollGroupPanelData {
  readonly clientId: string;
  readonly group: PayrollGroupDetailDto | null;
}

const hours = z.string().trim().regex(/^\d{1,2}(\.\d{1,2})?$/, 'Horas con hasta 2 decimales.');
const fraction = z.string().trim().regex(/^(0(\.\d{1,4})?|1(\.0{1,4})?)$/, 'Entre 0 y 1 (0.25 = 25 %).');
const multiplier = z.string().trim().regex(/^[1-5](\.\d{1,2})?$/, 'Entre 1 y 5.');

/**
 * «Nuevo grupo de nómina» / edit (DATA_MODEL §5.1): when its periods end, the shift that partial hours are measured
 * against, the daily attendance lock and the engine's group rules (séptimo día divisor, prima dominical, festivo laborado).
 */
@Component({
  selector: 'app-payroll-group-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.group ? 'Editar grupo de nómina' : 'Nuevo grupo de nómina' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="group-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="name" autocomplete="off" placeholder="Validadores 12 hrs" />
          <mat-error>{{ s.msg('name') }}</mat-error>
        </mat-form-field>
        <div class="row">
          <mat-form-field>
            <mat-label>Período</mat-label>
            <mat-select formControlName="periodType">
              @for (p of periodTypes; track p.value) {
                <mat-option [value]="p.value">{{ p.label }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('periodType') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Día de corte</mat-label>
            <mat-select formControlName="cutoffWeekday">
              @for (d of weekdays; track $index) {
                <mat-option [value]="$index">{{ d }}</mat-option>
              }
            </mat-select>
            <mat-hint>El día en que termina el período</mat-hint>
            <mat-error>{{ s.msg('cutoffWeekday') }}</mat-error>
          </mat-form-field>
        </div>
        <div class="row">
          <mat-form-field>
            <mat-label>Horas por turno</mat-label>
            <input matInput formControlName="shiftHours" inputmode="decimal" autocomplete="off" />
            <mat-error>{{ s.msg('shiftHours') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Cierre de asistencia</mat-label>
            <input matInput type="time" formControlName="attendanceLockTime" />
            <mat-hint>Cada día a esta hora</mat-hint>
            <mat-error>{{ s.msg('attendanceLockTime') }}</mat-error>
          </mat-form-field>
        </div>
        <div class="row">
          <mat-form-field>
            <mat-label>Divisor séptimo día</mat-label>
            <mat-select formControlName="seventhDayDivisor">
              @for (n of divisors; track n) {
                <mat-option [value]="n">{{ n }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('seventhDayDivisor') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Prima dominical</mat-label>
            <input matInput formControlName="sundayPremiumPct" inputmode="decimal" autocomplete="off" />
            <mat-hint>0.25 = 25 %</mat-hint>
            <mat-error>{{ s.msg('sundayPremiumPct') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Festivo laborado ×</mat-label>
            <input matInput formControlName="holidayWorkedMultiplier" inputmode="decimal" autocomplete="off" />
            <mat-error>{{ s.msg('holidayWorkedMultiplier') }}</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field>
          <mat-label>Horario (como se imprime en la prenómina)</mat-label>
          <input matInput formControlName="scheduleDescription" autocomplete="off" placeholder="Lunes a domingo 12 hrs corte viernes a jueves" />
          <mat-error>{{ s.msg('scheduleDescription') }}</mat-error>
        </mat-form-field>
        @if (data.group) {
          <mat-slide-toggle formControlName="isActive">Activo</mat-slide-toggle>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="group-form" [disabled]="s.busy()">Guardar grupo</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      mat-form-field {
        flex: 1 1 120px;
      }
    }
  `,
})
export class PayrollGroupPanel {
  readonly data = inject<PayrollGroupPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<PayrollGroupPanel>);
  private readonly groups = inject(PayrollGroupsRepository);
  private readonly snack = inject(MatSnackBar);

  readonly periodTypes = (Object.keys(PERIOD_TYPE) as PeriodType[]).map((value) => ({ value, label: PERIOD_TYPE[value] }));
  readonly weekdays = WEEKDAYS_FROM_SUNDAY;
  readonly divisors = [1, 2, 3, 4, 5, 6, 7];

  private readonly g = this.data.group;
  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.g?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    periodType: [(this.g?.periodType ?? 'weekly') as PeriodType],
    cutoffWeekday: [Number(this.g?.cutoffWeekday ?? 4)],
    shiftHours: [String(this.g?.shiftHours ?? '12'), [Validators.required, zodValidator(hours)]],
    attendanceLockTime: [(this.g?.attendanceLockTime ?? '23:59:00').slice(0, 5), Validators.required],
    seventhDayDivisor: [Number(this.g?.seventhDayDivisor ?? 6)],
    sundayPremiumPct: [String(this.g?.sundayPremiumPct ?? '0.25'), [Validators.required, zodValidator(fraction)]],
    holidayWorkedMultiplier: [String(this.g?.holidayWorkedMultiplier ?? '2'), [Validators.required, zodValidator(multiplier)]],
    scheduleDescription: [this.g?.scheduleDescription ?? '', Validators.maxLength(120)],
    isActive: [this.g?.isActive ?? true],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const saved = await this.groups.save(this.g?.id ?? null, {
        clientId: this.data.clientId,
        name: v.name.trim(),
        periodType: v.periodType,
        cutoffWeekday: v.cutoffWeekday,
        shiftHours: v.shiftHours.trim(),
        attendanceLockTime: `${v.attendanceLockTime}:00`,
        seventhDayDivisor: v.seventhDayDivisor,
        sundayPremiumPct: v.sundayPremiumPct.trim(),
        holidayWorkedMultiplier: v.holidayWorkedMultiplier.trim(),
        scheduleDescription: v.scheduleDescription.trim() || null,
        timeZone: this.g?.timeZone ?? null,
        isActive: v.isActive,
      });
      this.snack.open(`Grupo ${saved.name} guardado`, undefined, { duration: 4000 });
      this.ref.close(saved);
    });
  }
}
