import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatCheckbox } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { type DateKey, formatDateKey, todayKey } from '@platform/shared';
import type { EmployeeDetailDto, EmployeeStatus } from '../data/api-types';
import { PERIOD_STATUS_LABEL } from '../data/payroll-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { PanelState } from '../clients/panel-base';

export interface StatusPanelData {
  readonly employee: EmployeeDetailDto;
  readonly terminationReasons: readonly string[];
}

type Choice = Exclude<EmployeeStatus, 'active'> | 'reactivate';

/**
 * «Cambiar estatus» (SPEC §5.1): Vacaciones (inicio y fin), Incapacidad (fin estimado, editable después; folio IMSS
 * opcional), Suspensión (fin y motivo), Baja (fecha, motivo del catálogo, confirmación — irreversible salvo Reingreso) and
 * Reactivar, which ends the current period today. Each period fills the attendance of its days.
 */
@Component({
  selector: 'app-status-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButtonToggleGroup, MatButtonToggle, MatCheckbox, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Cambiar estatus</h2>
    <mat-dialog-content>
      <p class="muted">{{ e.fullName }}@if (current(); as p) { · {{ label[p.status] }} {{ range(p) }} }</p>
      <mat-button-toggle-group class="choices" [value]="choice()" (change)="pick($event.value)" aria-label="Estatus" hideSingleSelectionIndicator>
        @for (c of choices(); track c.value) {
          <mat-button-toggle [value]="c.value">{{ c.label }}</mat-button-toggle>
        }
      </mat-button-toggle-group>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="status-form" novalidate>
        @if (choice() === 'reactivate') {
          @if (current(); as p) {
            <p>
              @if (p.startDate >= today) {
                {{ label[p.status] }} <strong>se cancela completo</strong>: no hay días tomados antes de hoy.
              } @else {
                {{ label[p.status] }} termina ayer, <strong>{{ format(yesterday) }}</strong>; regresa hoy como Activo.
              }
              Los días de asistencia que quedaban se quitan.
            </p>
          }
        } @else if (editingCurrent()) {
          <mat-form-field>
            <mat-label>{{ choice() === 'sick_leave' ? 'Fin estimado' : 'Fecha de fin' }}</mat-label>
            <input matInput type="date" formControlName="endDate" />
            <mat-hint>Cambia el fin del período actual (inicio {{ format(current()!.startDate) }})</mat-hint>
            <mat-error>{{ s.msg('endDate') }}</mat-error>
          </mat-form-field>
          @if (choice() === 'sick_leave') {
            <mat-form-field>
              <mat-label>Folio IMSS (opcional)</mat-label>
              <input matInput formControlName="reference" autocomplete="off" />
              <mat-error>{{ s.msg('reference') }}</mat-error>
            </mat-form-field>
          }
        } @else {
          <div class="row">
            <mat-form-field>
              <mat-label>{{ choice() === 'terminated' ? 'Fecha de baja' : 'Fecha de inicio' }}</mat-label>
              <input matInput type="date" formControlName="startDate" />
              <mat-error>{{ s.msg('startDate') }}</mat-error>
            </mat-form-field>
            @if (choice() !== 'terminated') {
              <mat-form-field>
                <mat-label>{{ choice() === 'sick_leave' ? 'Fin estimado' : 'Fecha de fin' }}</mat-label>
                <input matInput type="date" formControlName="endDate" />
                <mat-error>{{ s.msg('endDate') }}</mat-error>
              </mat-form-field>
            }
          </div>
          @if (choice() === 'sick_leave') {
            <mat-form-field>
              <mat-label>Folio IMSS (opcional)</mat-label>
              <input matInput formControlName="reference" autocomplete="off" />
              <mat-hint>No se guarda información médica</mat-hint>
              <mat-error>{{ s.msg('reference') }}</mat-error>
            </mat-form-field>
          }
          @if (choice() === 'suspended') {
            <mat-form-field>
              <mat-label>Motivo</mat-label>
              <input matInput formControlName="reason" autocomplete="off" />
              <mat-error>{{ s.msg('reason') }}</mat-error>
            </mat-form-field>
          }
          @if (choice() === 'terminated') {
            <mat-form-field>
              <mat-label>Motivo de la baja</mat-label>
              <mat-select formControlName="reason">
                @for (r of data.terminationReasons; track r) {
                  <mat-option [value]="r">{{ r }}</mat-option>
                }
              </mat-select>
              <mat-error>{{ s.msg('reason') }}</mat-error>
            </mat-form-field>
            <mat-checkbox formControlName="confirm">Confirmo la baja. Solo se revierte con un Reingreso.</mat-checkbox>
          }
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="status-form" [disabled]="s.busy() || (choice() === 'terminated' && !confirmed())"
        [class.destructive]="choice() === 'terminated'">
        {{ choice() === 'reactivate' ? 'Reactivar' : choice() === 'terminated' ? 'Dar de baja' : 'Guardar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .choices {
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
    .row {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      mat-form-field {
        flex: 1 1 160px;
      }
    }
  `,
})
export class StatusPanel {
  readonly data = inject<StatusPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<StatusPanel>);
  private readonly employees = inject(EmployeesRepository);
  private readonly snack = inject(MatSnackBar);
  readonly e = this.data.employee;
  readonly label = PERIOD_STATUS_LABEL;
  readonly format = formatDateKey;
  readonly today = todayKey();
  readonly yesterday = new Date(Date.parse(this.today) - 86_400_000).toISOString().slice(0, 10) as DateKey;

  /** The period behind today's status, or the next scheduled one (it can be moved or cancelled). */
  readonly current = signal(this.e.period ?? this.e.nextPeriod ?? null);
  readonly choice = signal<Choice>(this.e.period ? 'reactivate' : 'vacation');
  readonly choices = computed(() => [
    ...(this.current() && this.current()!.status !== 'terminated' ? [{ value: 'reactivate' as Choice, label: 'Reactivar' }] : []),
    { value: 'vacation' as Choice, label: 'Vacaciones' },
    { value: 'sick_leave' as Choice, label: 'Incapacidad' },
    { value: 'suspended' as Choice, label: 'Suspensión' },
    { value: 'terminated' as Choice, label: 'Baja' },
  ]);
  /** Choosing the current period's own status edits its end (an incapacidad that lasts longer) instead of adding one. */
  readonly editingCurrent = computed(() => this.current()?.status === this.choice() && this.choice() !== 'terminated');

  readonly form = inject(FormBuilder).nonNullable.group({
    startDate: [this.today as string, Validators.required],
    endDate: [(this.current()?.endDate ?? '') as string],
    reason: [''],
    reference: [this.current()?.reference ?? ''],
    confirm: [false],
  });
  readonly s = new PanelState(this.form);
  readonly confirmed = toSignal(this.form.controls.confirm.valueChanges, { initialValue: false });

  range(p: { startDate: string; endDate: string | null }): string {
    return p.endDate ? `${formatDateKey(p.startDate as DateKey)} – ${formatDateKey(p.endDate as DateKey)}` : `desde ${formatDateKey(p.startDate as DateKey)}`;
  }

  pick(choice: Choice): void {
    this.choice.set(choice);
    this.s.alert.set(null);
    this.form.controls.endDate.setValue(this.editingCurrent() ? (this.current()!.endDate ?? '') : '');
  }

  save(): Promise<void> {
    const choice = this.choice();
    if (this.editingCurrent() && !this.form.controls.endDate.value) {
      this.s.alert.set('Indica la fecha de fin.');
      return Promise.resolve();
    }
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      if (choice === 'reactivate') await this.employees.endStatus(this.e.id, this.current()!.id);
      else if (this.editingCurrent()) await this.employees.updateStatus(this.e.id, this.current()!.id, v.endDate as DateKey, text(v.reference));
      else
        await this.employees.addStatus(this.e.id, {
          status: choice,
          startDate: v.startDate as DateKey,
          endDate: choice === 'terminated' ? null : ((text(v.endDate) as DateKey | null) ?? null),
          reason: text(v.reason),
          reference: text(v.reference),
        });
      this.snack.open(choice === 'reactivate' ? `${this.e.fullName} está Activo` : 'Estatus guardado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
