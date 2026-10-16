import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { openSidePanel } from '@platform/ui';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { ClientsRepository } from '../data/repositories';
import { PayrollGroupCard } from './payroll-group-card';
import { PayrollGroupPanel, type PayrollGroupPanelData } from './payroll-group-panel';

/**
 * «Detalle del cliente (nómina)» (SPEC §5.6): its grupos de nómina, each with its puestos and rates — the only place rates
 * are edited — and its bonuses.
 */
@Component({
  selector: 'app-payroll-client-detail',
  imports: [MatButton, MatIcon, MatProgressBar, RouterLink, PayrollGroupCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton routerLink="/clientes-nomina" class="back">
        <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>Clientes (nómina)
      </a>
      @if (client.loading() && !client.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (client.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (client.value(); as c) {
        <header class="page-header">
          <h1>
            <span class="eyebrow">Detalle del cliente (nómina)</span>
            {{ c.shortName }}
            <span class="subtitle">{{ c.legalName ?? '' }}@if (c.rfc) { · <span class="mono">{{ c.rfc }}</span> }</span>
          </h1>
        </header>
        <div class="section-head">
          <h2>Grupos de nómina</h2>
          <button matButton="filled" type="button" (click)="newGroup()">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nuevo grupo
          </button>
        </div>
        @for (g of groups.value() ?? []; track g.id) {
          <app-payroll-group-card [summary]="g" />
        } @empty {
          @if (groups.value()) {
            <div class="empty-state">
              <mat-icon fontSet="material-symbols-outlined">groups</mat-icon>
              <p><strong>Este cliente aún no tiene grupos de nómina</strong></p>
              <p>Un grupo define el período, el día de corte, las horas por turno y los puestos con sus tarifas.</p>
            </div>
          }
        }
      }
    </div>
  `,
  styles: `
    .back {
      margin: -8px 0 8px -12px;
    }
    .eyebrow {
      display: block;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .subtitle {
      display: block;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .section-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 12px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-large);
      }
    }
  `,
})
export class PayrollClientDetail {
  /** Route param, read from the snapshot: the live query starts in the field initializer, before inputs are bound. */
  private readonly clientId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly id = () => this.clientId;
  readonly client = inject(ClientsRepository).detail('payroll', () => this.id());
  readonly groups = inject(PayrollGroupsRepository).list(() => this.id());
  private readonly dialog = inject(MatDialog);

  newGroup(): void {
    openSidePanel<PayrollGroupPanel, PayrollGroupPanelData>(this.dialog, PayrollGroupPanel, { clientId: this.clientId, group: null });
  }
}
