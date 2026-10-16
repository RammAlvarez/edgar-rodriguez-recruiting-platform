import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, type MatDialog, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

export interface PromptData {
  readonly title: string;
  readonly body?: string;
  readonly label: string;
  readonly confirm: string;
  /** `date` asks for a yyyy-MM-dd (fecha de ingreso); `text` for a required reason. */
  readonly kind: 'text' | 'date';
  readonly initial?: string;
  readonly destructive?: boolean;
}

/** One required value before an action: the motivo of Pausar / Cancelar / No apto / Descartado, or the fecha de ingreso. */
@Component({
  selector: 'app-prompt-dialog',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatInput, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.body) {
        <p>{{ data.body }}</p>
      }
      <form (ngSubmit)="ok()" id="prompt-form" novalidate>
        <mat-form-field class="field">
          <mat-label>{{ data.label }}</mat-label>
          @if (data.kind === 'date') {
            <input matInput type="date" [formControl]="value" />
          } @else {
            <textarea matInput [formControl]="value" rows="2" cdkFocusInitial></textarea>
          }
          <mat-error>Este campo es obligatorio.</mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close(null)">Cancelar</button>
      <button matButton="filled" type="submit" form="prompt-form" [class.destructive]="data.destructive">{{ data.confirm }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .field {
      width: 100%;
    }
  `,
})
export class PromptDialog {
  readonly data = inject<PromptData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<PromptDialog, string | null>);
  readonly value = new FormControl(this.data.initial ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] });

  ok(): void {
    this.value.markAsTouched();
    if (this.value.value.trim() === '') {
      this.value.setErrors({ required: true });
      return;
    }
    this.ref.close(this.value.value.trim());
  }
}

/** Resolves to the typed value, or null when cancelled. */
export function prompt(dialog: MatDialog, data: PromptData): Promise<string | null> {
  return new Promise((resolve) =>
    dialog
      .open<PromptDialog, PromptData, string | null>(PromptDialog, { data, width: 'min(440px, 100vw)' })
      .afterClosed()
      .subscribe((r) => resolve(r ?? null)),
  );
}
