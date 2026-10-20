import type { ComponentType } from '@angular/cdk/portal';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { merge } from 'rxjs';
import { filter } from 'rxjs/operators';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  type MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  type MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';

/**
 * The prototype's side panel (create and edit share it, ARCHITECTURE §9.12): a dialog docked to the right edge, full
 * height, 480px wide (full width on phones). Focus trap, Esc and the scrim come from MatDialog; closing through them
 * asks first when the panel's `form` has unsaved edits.
 */
export function openSidePanel<C, D, R = unknown>(dialog: MatDialog, component: ComponentType<C>, data: D): MatDialogRef<C, R> {
  const ref = dialog.open<C, D, R>(component, {
    data,
    position: { right: '0', top: '0' },
    height: '100dvh',
    width: 'min(480px, 100vw)',
    maxWidth: '100vw',
    maxHeight: '100dvh',
    panelClass: 'ui-side-panel',
    autoFocus: 'first-tabbable',
    disableClose: true,
  });
  let asking = false;
  merge(ref.backdropClick(), ref.keydownEvents().pipe(filter((e) => e.key === 'Escape'))).subscribe(async () => {
    if (asking) return;
    const form = (ref.componentInstance as { form?: { dirty?: boolean } } | null)?.form;
    if (form?.dirty) {
      asking = true;
      const discard = await confirm(dialog, {
        title: '¿Descartar los cambios?',
        body: 'Lo que capturaste no se ha guardado y se perderá.',
        confirm: 'Descartar',
        destructive: true,
      });
      asking = false;
      if (!discard) return;
    }
    ref.close();
  });
  return ref;
}

export interface ConfirmData {
  readonly title: string;
  readonly body: string;
  readonly confirm: string;
  readonly destructive?: boolean;
}

/** Question title + consequence body; dismissive action first, confirming last, named for the action (design system). */
@Component({
  selector: 'ui-confirm-dialog',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.body }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" [mat-dialog-close]="false">Cancelar</button>
      <button matButton="filled" type="button" [class.destructive]="data.destructive" [mat-dialog-close]="true">
        {{ data.confirm }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialog {
  readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
}

export function confirm(dialog: MatDialog, data: ConfirmData): Promise<boolean> {
  return new Promise((resolve) =>
    dialog
      .open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, { data, width: 'min(420px, 100vw)' })
      .afterClosed()
      .subscribe((r) => resolve(r === true)),
  );
}
