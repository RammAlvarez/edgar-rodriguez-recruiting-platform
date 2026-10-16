import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatTooltip } from '@angular/material/tooltip';
import { API_CLIENT } from './api';

/** «Actualizar»: re-fetches every live query, for changes other users made (there is no realtime, D39). */
@Component({
  selector: 'ui-refresh-button',
  imports: [MatIconButton, MatIcon, MatTooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button matIconButton type="button" aria-label="Actualizar" matTooltip="Actualizar" (click)="refresh()">
      <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon>
    </button>
  `,
})
export class RefreshButton {
  private readonly api = inject(API_CLIENT);

  refresh(): void {
    this.api.cache.refreshAll();
  }
}
