import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel, MatPrefix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { decimalString, type DateKey, formatDateKey, todayKey } from '@platform/shared';
import { zodValidator } from '@platform/ui';
import { z } from 'zod';
import type { PositionDto } from '../data/api-types';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { PanelState } from './panel-base';

export interface PositionPanelData {
  readonly groupId: string;
  readonly groupName: string;
  /** new: name and first rates · rates: «Cambiar tarifas» from a date · edit: name and active. */
  readonly mode: 'new' | 'rates' | 'edit';
  readonly position: PositionDto | null;
}

/** COSTO X HRA keeps 4 decimals (35.7142); the other rates are pesos with cents. */
const hourly = z.string().trim().regex(/^\d{1,6}(\.\d{1,4})?$/, 'Importe con hasta 4 decimales, por ejemplo 35.7142.');

/**
 * Puestos of a group and their rates (DATA_MODEL §5.2, PLAN E12): new rates apply from a date and the previous ones end the
 * day before, so past periods keep the rates they were paid at.
 */
@Component({
  selector: 'app-position-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatPrefix, MatInput, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ title }}</h2>
    <mat-dialog-content>
      <p class="muted context">{{ data.groupName }}@if (data.position) { · {{ data.position.name }} }</p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="position-form" novalidate>
        @if (data.mode !== 'rates') {
          <mat-form-field>
            <mat-label>Puesto</mat-label>
            <input matInput formControlName="name" autocomplete="off" placeholder="VALIDADOR" />
            <mat-error>{{ s.msg('name') }}</mat-error>
          </mat-form-field>
        }
        @if (data.mode !== 'edit') {
          <div class="row">
            <mat-form-field>
              <mat-label>SD bolsillo</mat-label>
              <span matTextPrefix>$&nbsp;</span>
              <input matInput formControlName="dailySalaryPocket" inputmode="decimal" autocomplete="off" />
              <mat-hint>Sueldo real diario</mat-hint>
              <mat-error>{{ s.msg('dailySalaryPocket') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>SD IMSS</mat-label>
              <span matTextPrefix>$&nbsp;</span>
              <input matInput formControlName="dailySalaryImss" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('dailySalaryImss') }}</mat-error>
            </mat-form-field>
          </div>
          <div class="row">
            <mat-form-field>
              <mat-label>Costo por hora</mat-label>
              <span matTextPrefix>$&nbsp;</span>
              <input matInput formControlName="hourlyRate" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('hourlyRate') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Bono días trabajados</mat-label>
              <span matTextPrefix>$&nbsp;</span>
              <input matInput formControlName="daysWorkedBonus" inputmode="decimal" autocomplete="off" />
              <mat-error>{{ s.msg('daysWorkedBonus') }}</mat-error>
            </mat-form-field>
          </div>
          <mat-form-field>
            <mat-label>Vigentes desde</mat-label>
            <input matInput type="date" formControlName="validFrom" />
            @if (data.mode === 'rates' && data.position?.current; as c) {
              <mat-hint>Las tarifas actuales rigen desde el {{ formatDate(c.validFrom) }}; terminarán un día antes de esta fecha.</mat-hint>
            }
            <mat-error>{{ s.msg('validFrom') }}</mat-error>
          </mat-form-field>
        }
        @if (data.mode === 'edit') {
          <mat-slide-toggle formControlName="isActive">Activo (se ofrece en altas)</mat-slide-toggle>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="position-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .context {
      margin: 0 0 12px;
    }
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
export class PositionPanel {
  readonly data = inject<PositionPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<PositionPanel>);
  private readonly groups = inject(PayrollGroupsRepository);
  private readonly snack = inject(MatSnackBar);
  readonly formatDate = formatDateKey;

  readonly title = { new: 'Nuevo puesto', rates: 'Cambiar tarifas', edit: 'Editar puesto' }[this.data.mode];

  private readonly current = this.data.position?.current;
  private readonly money = (v: unknown) => (v == null ? '' : String(v));
  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.data.position?.name ?? '', this.data.mode === 'rates' ? [] : [Validators.required, Validators.maxLength(60)]],
    dailySalaryPocket: [this.money(this.current?.dailySalaryPocket), this.data.mode === 'edit' ? [] : [Validators.required, zodValidator(decimalString())]],
    dailySalaryImss: [this.money(this.current?.dailySalaryImss), this.data.mode === 'edit' ? [] : [Validators.required, zodValidator(decimalString())]],
    hourlyRate: [this.money(this.current?.hourlyRate), this.data.mode === 'edit' ? [] : [Validators.required, zodValidator(hourly)]],
    daysWorkedBonus: [this.money(this.current?.daysWorkedBonus ?? '0'), this.data.mode === 'edit' ? [] : [Validators.required, zodValidator(decimalString())]],
    validFrom: [todayKey() as string, this.data.mode === 'edit' ? [] : Validators.required],
    isActive: [this.data.position?.isActive ?? true],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const rates = {
        dailySalaryPocket: v.dailySalaryPocket.trim(),
        dailySalaryImss: v.dailySalaryImss.trim(),
        hourlyRate: v.hourlyRate.trim(),
        daysWorkedBonus: v.daysWorkedBonus.trim(),
      };
      if (this.data.mode === 'new') await this.groups.addPosition(this.data.groupId, { name: v.name.trim(), ...rates, validFrom: v.validFrom as DateKey });
      else if (this.data.mode === 'rates') await this.groups.changeRates(this.data.position!.id, { ...rates, validFrom: v.validFrom as DateKey });
      else await this.groups.updatePosition(this.data.position!.id, v.name.trim(), v.isActive);
      this.snack.open(this.data.mode === 'rates' ? 'Tarifas guardadas' : 'Puesto guardado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
