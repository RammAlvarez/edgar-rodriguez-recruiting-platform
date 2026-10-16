import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { REGISTRATION_BANK } from '../data/payroll-labels';
import { PrepayrollRepository } from '../data/payroll-repositories';

/**
 * «Tarjetas por solicitar» (SPEC §5.5): who is waiting for a payroll card, by bank. The alta files that send them to the bank
 * are the next delivery (S10); a request is left out once the person has a principal account.
 */
@Component({
  selector: 'app-card-queue-panel',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatButton, MatProgressBar, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Tarjetas por solicitar</h2>
    <mat-dialog-content>
      @if (queue.loading() && !queue.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @for (r of queue.value() ?? []; track r.id) {
        <div class="row">
          <div>
            <a [routerLink]="['/personal', r.employeeId]" (click)="ref.close()">{{ r.fullName }}</a>
            <div class="muted">{{ r.clientName }} · {{ r.groupName }} · desde {{ since(r.requestedAt) }}</div>
          </div>
          <span class="pill">{{ bank[r.bank!] }}</span>
        </div>
      } @empty {
        @if (queue.value()) {
          <p class="list-empty">Nadie espera tarjeta.</p>
        }
      }
      <p class="muted note">Los archivos de alta para el banco llegan en la siguiente entrega.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 10px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .note {
      margin-top: 12px;
    }
  `,
})
export class CardQueuePanel {
  readonly ref = inject(MatDialogRef<CardQueuePanel>);
  readonly queue = inject(PrepayrollRepository).cardQueue();
  readonly bank = REGISTRATION_BANK;

  since(at: string | Date): string {
    return new Date(at).toLocaleDateString('es-MX', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'America/Monterrey' });
  }
}
