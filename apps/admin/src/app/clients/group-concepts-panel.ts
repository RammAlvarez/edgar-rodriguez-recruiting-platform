import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { decimalString } from '@platform/shared';
import { zodValidator } from '@platform/ui';
import type { GroupConceptDto } from '../data/api-types';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { PanelState } from './panel-base';

export interface GroupConceptsPanelData {
  readonly groupId: string;
  readonly groupName: string;
  readonly concepts: readonly GroupConceptDto[];
}

/**
 * The concepts a group switches on (D63): the bonuses — off until the client defines its scheme (SPEC Q4), as SEM 38 pays —
 * and descanso laborado. «Bono días trabajados» without an amount pays each puesto's.
 */
@Component({
  selector: 'app-group-concepts-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Bonos y conceptos</h2>
    <mat-dialog-content>
      <p class="muted">{{ data.groupName }}. Los bonos están apagados mientras el cliente no defina su esquema.</p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="concepts-form" novalidate>
        <div formArrayName="concepts">
          @for (c of data.concepts; track c.code; let i = $index) {
            <section class="concept" [formGroupName]="i">
              <mat-slide-toggle formControlName="isActive">{{ c.name }}</mat-slide-toggle>
              <div class="row">
                <mat-form-field>
                  <mat-label>{{ c.calc === 'per_day_worked' ? 'Importe por base' : 'Importe' }}</mat-label>
                  <input matInput formControlName="amount" inputmode="decimal" autocomplete="off" />
                  @if (c.code === 'BONUS_DAYS_WORKED') {
                    <mat-hint>Vacío = el bono de cada puesto</mat-hint>
                  }
                  <mat-error>{{ s.msg('concepts.' + i + '.amount') }}</mat-error>
                </mat-form-field>
                @if (c.calc === 'per_day_worked') {
                  <mat-form-field>
                    <mat-label>Base (días)</mat-label>
                    <input matInput formControlName="daysBasis" inputmode="decimal" autocomplete="off" />
                    <mat-hint>{{ c.code === 'BONUS_PUNCTUALITY' ? '400 / 7 × días' : 'importe / base × días' }}</mat-hint>
                    <mat-error>{{ s.msg('concepts.' + i + '.daysBasis') }}</mat-error>
                  </mat-form-field>
                }
              </div>
            </section>
          }
        </div>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="concepts-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .concept {
      padding: 12px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .row {
      display: flex;
      gap: 12px;
      margin-top: 8px;
      mat-form-field {
        flex: 1 1 120px;
      }
    }
  `,
})
export class GroupConceptsPanel {
  readonly data = inject<GroupConceptsPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<GroupConceptsPanel>);
  private readonly groups = inject(PayrollGroupsRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder).nonNullable;

  readonly form = this.fb.group({
    concepts: new FormArray(
      this.data.concepts.map((c) =>
        this.fb.group({
          isActive: [c.isActive],
          amount: [c.amountFromPosition ? '' : String(c.amount ?? ''), zodValidator(decimalString())],
          daysBasis: [String(c.daysBasis ?? '')],
        }),
      ),
    ),
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const values = this.form.getRawValue().concepts;
      await this.groups.saveConcepts(
        this.data.groupId,
        this.data.concepts.map((c, i) => ({
          code: c.code,
          isActive: values[i].isActive,
          amount: values[i].amount.trim() || null,
          daysBasis: c.calc === 'per_day_worked' ? values[i].daysBasis.trim() || null : null,
        })),
      );
      this.snack.open('Conceptos guardados', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
