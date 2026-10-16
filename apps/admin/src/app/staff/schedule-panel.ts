import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { DateKey } from '@platform/shared';
import type { SiteOptionDto, WorkScheduleDto } from '../data/api-types';
import { ISO_WEEKDAYS } from '../data/payroll-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { PanelState } from '../clients/panel-base';

export interface SchedulePanelData {
  readonly employeeId: string;
  readonly employeeName: string;
  readonly sites: readonly SiteOptionDto[];
  readonly schedule: WorkScheduleDto | null;
}

/**
 * «Horario» (SPEC §5.2): a sitio of the employee's client and the hours, either every week on some days («Limpieza oficinas
 * HEB, L–V 07:00–15:00») or once on a date («mantenimiento 12/10/2026»). The Calendario shows them by day.
 */
@Component({
  selector: 'app-schedule-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButtonToggleGroup, MatButtonToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.schedule ? 'Editar horario' : 'Nuevo horario' }}</h2>
    <mat-dialog-content>
      <p class="muted">{{ data.employeeName }}</p>
      <mat-button-toggle-group [value]="kind()" (change)="kind.set($event.value)" aria-label="Tipo de horario" hideSingleSelectionIndicator>
        <mat-button-toggle value="weekly">Cada semana</mat-button-toggle>
        <mat-button-toggle value="once">Una sola vez</mat-button-toggle>
      </mat-button-toggle-group>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="schedule-form" novalidate>
        <mat-form-field>
          <mat-label>Sitio</mat-label>
          <mat-select formControlName="clientSiteId">
            @for (s of data.sites; track s.id) {
              <mat-option [value]="s.id">{{ s.name }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('clientSiteId') }}</mat-error>
        </mat-form-field>
        @if (kind() === 'weekly') {
          <div class="days" role="group" aria-label="Días">
            @for (d of weekdays; track d.value) {
              <button type="button" class="day" [class.on]="days().includes(d.value)" [attr.aria-pressed]="days().includes(d.value)"
                [attr.aria-label]="d.label" (click)="toggle(d.value)">{{ d.short }}</button>
            }
          </div>
        } @else {
          <mat-form-field>
            <mat-label>Fecha</mat-label>
            <input matInput type="date" formControlName="oneOffDate" />
            <mat-error>{{ s.msg('oneOffDate') }}</mat-error>
          </mat-form-field>
        }
        <div class="row">
          <mat-form-field>
            <mat-label>Entrada</mat-label>
            <input matInput type="time" formControlName="startTime" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Salida</mat-label>
            <input matInput type="time" formControlName="endTime" />
            <mat-hint>Antes que la entrada = termina al día siguiente</mat-hint>
            <mat-error>{{ s.msg('endTime') }}</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field>
          <mat-label>Trabajo (opcional)</mat-label>
          <input matInput formControlName="jobDescription" autocomplete="off" placeholder="Mantenimiento A/C" />
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="schedule-form" [disabled]="s.busy()">Guardar horario</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-button-toggle-group {
      margin-bottom: 12px;
    }
    .days {
      display: flex;
      gap: 6px;
      margin-bottom: 16px;
    }
    .day {
      all: unset;
      box-sizing: border-box;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      text-align: center;
      line-height: 40px;
      cursor: pointer;
      font: var(--mat-sys-label-large);
      border: 1px solid var(--mat-sys-outline);
      &.on {
        background: var(--mat-sys-primary);
        color: var(--mat-sys-on-primary);
        border-color: var(--mat-sys-primary);
      }
      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }
    .row {
      display: flex;
      gap: 12px;
      mat-form-field {
        flex: 1 1 120px;
      }
    }
  `,
})
export class SchedulePanel {
  readonly data = inject<SchedulePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<SchedulePanel>);
  private readonly employees = inject(EmployeesRepository);
  private readonly snack = inject(MatSnackBar);
  readonly weekdays = ISO_WEEKDAYS;
  private readonly sc = this.data.schedule;

  readonly kind = signal<'weekly' | 'once'>(this.sc?.oneOffDate ? 'once' : 'weekly');
  readonly days = signal<number[]>(this.sc ? this.sc.weekdays.map(Number) : [1, 2, 3, 4, 5]);

  readonly form = inject(FormBuilder).nonNullable.group({
    clientSiteId: [this.sc?.clientSiteId ?? this.data.sites[0]?.id ?? (null as string | null), Validators.required],
    oneOffDate: [(this.sc?.oneOffDate ?? '') as string],
    startTime: [(this.sc?.startTime ?? '07:00:00').slice(0, 5), Validators.required],
    endTime: [(this.sc?.endTime ?? '15:00:00').slice(0, 5), Validators.required],
    jobDescription: [this.sc?.jobDescription ?? '', Validators.maxLength(120)],
  });
  readonly s = new PanelState(this.form);

  toggle(day: number): void {
    this.days.update((d) => (d.includes(day) ? d.filter((x) => x !== day) : [...d, day].sort((a, b) => a - b)));
  }

  save(): Promise<void> {
    if (this.kind() === 'weekly' && this.days().length === 0) {
      this.s.alert.set('Elige al menos un día.');
      return Promise.resolve();
    }
    if (this.kind() === 'once' && !this.form.controls.oneOffDate.value) {
      this.s.alert.set('Elige la fecha.');
      return Promise.resolve();
    }
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      await this.employees.saveSchedule(this.data.employeeId, this.sc?.id ?? null, {
        clientSiteId: v.clientSiteId!,
        weekdays: this.kind() === 'weekly' ? this.days() : [],
        oneOffDate: this.kind() === 'once' ? (v.oneOffDate as DateKey) : null,
        startTime: `${v.startTime}:00`,
        endTime: `${v.endTime}:00`,
        validFrom: null,
        validTo: null,
        jobDescription: v.jobDescription.trim() || null,
      });
      this.snack.open('Horario guardado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
