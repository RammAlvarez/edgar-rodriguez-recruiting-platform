import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { InterviewDto } from '../data/api-types';
import { OUTCOME, type InterviewOutcome } from '../data/recruiting-labels';
import { CandidatesRepository, RecruitersRepository } from '../data/recruiting-repositories';
import { PanelState } from '../clients/panel-base';

export interface InterviewPanelData {
  readonly candidateId: string;
  readonly interview: InterviewDto | null;
}

/** "yyyy-MM-ddTHH:mm" in the browser's zone for a datetime-local input, and back to an ISO instant. */
const toLocalInput = (d: Date) => {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

/** «Programar entrevista» / edit (DATA_MODEL §4.5): fecha y hora, entrevistador, lugar, resultado, notas. */
@Component({
  selector: 'app-interview-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.interview ? 'Editar entrevista' : 'Programar entrevista' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="interview-form" novalidate>
        <mat-form-field>
          <mat-label>Fecha y hora</mat-label>
          <input matInput type="datetime-local" formControlName="scheduledAt" />
          <mat-error>{{ s.msg('scheduledAt') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Entrevistador</mat-label>
          <mat-select formControlName="interviewerId">
            <mat-option [value]="null">Sin asignar</mat-option>
            @for (u of staff(); track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('interviewerId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Lugar</mat-label>
          <input matInput formControlName="location" autocomplete="off" />
          <mat-hint>Dirección, sucursal o «Videollamada»</mat-hint>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Resultado</mat-label>
          <mat-select formControlName="outcome">
            @for (o of outcomes; track o.value) {
              <mat-option [value]="o.value">{{ o.label }}</mat-option>
            }
          </mat-select>
          <mat-hint>La etapa del candidato se cambia en el Pipeline</mat-hint>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Notas</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="interview-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
})
export class InterviewPanel {
  readonly data = inject<InterviewPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<InterviewPanel, boolean>);
  private readonly candidates = inject(CandidatesRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly staffList = inject(RecruitersRepository).staff();
  readonly staff = computed(() => this.staffList.value() ?? []);
  readonly outcomes = (Object.keys(OUTCOME) as InterviewOutcome[]).map((value) => ({ value, label: OUTCOME[value] }));

  private readonly i = this.data.interview;
  readonly form = inject(FormBuilder).group({
    scheduledAt: [this.i ? toLocalInput(new Date(this.i.scheduledAt)) : '', Validators.required],
    interviewerId: [this.i?.interviewerId ?? (null as string | null)],
    location: [this.i?.location ?? '', Validators.maxLength(200)],
    outcome: [(this.i?.outcome ?? 'pending') as InterviewOutcome],
    notes: [this.i?.notes ?? '', Validators.maxLength(2000)],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string | null | undefined) => (x == null || x.trim() === '' ? null : x.trim());
      await this.candidates.saveInterview(this.data.candidateId, this.i?.id ?? null, {
        scheduledAt: new Date(v.scheduledAt!),
        interviewerId: v.interviewerId ?? null,
        location: text(v.location),
        outcome: v.outcome!,
        notes: text(v.notes),
      });
      this.snack.open(this.i ? 'Entrevista actualizada' : 'Entrevista programada', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
