import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatRadioButton, MatRadioGroup } from '@angular/material/radio';
import type { OpeningRecruiterDto } from '../data/api-types';

export interface NewLeadData {
  readonly opening: string;
  readonly leaving: string;
  readonly candidates: readonly OpeningRecruiterDto[];
}

/** «Quitar» on a líder: pick the new líder among the other assigned recruiters (PLAN D71). Resolves to the user id. */
@Component({
  selector: 'app-new-lead-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatRadioGroup, MatRadioButton, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>¿Quién queda como líder?</h2>
    <mat-dialog-content>
      <p>{{ data.leaving }} deja {{ data.opening }}. Elige quién queda como líder entre los reclutadores asignados.</p>
      <mat-radio-group [value]="choice()" (change)="choice.set($event.value)" class="options" aria-label="Nuevo líder">
        @for (r of data.candidates; track r.userId) {
          <mat-radio-button [value]="r.userId">{{ r.displayName }}</mat-radio-button>
        }
      </mat-radio-group>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close(null)">Cancelar</button>
      <button matButton="filled" type="button" class="destructive" [disabled]="!choice()" (click)="ref.close(choice())">Quitar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .options {
      display: flex;
      flex-direction: column;
    }
  `,
})
export class NewLeadDialog {
  readonly data = inject<NewLeadData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<NewLeadDialog, string | null>);
  readonly choice = signal<string | null>(null);
}
