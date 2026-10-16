import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ClientsRepository } from '../data/repositories';

/** «Detalle del cliente (nómina)»: Grupos de nómina and Puestos, where the rates live (S7). */
@Component({
  selector: 'app-payroll-client-detail',
  imports: [MatTabGroup, MatTab, MatButton, MatIcon, MatProgressBar, RouterLink],
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
        <mat-tab-group animationDuration="0ms">
          <mat-tab label="Grupos de nómina">
            <div class="empty-state">
              <mat-icon fontSet="material-symbols-outlined">groups</mat-icon>
              <p><strong>Este cliente aún no tiene grupos de nómina</strong></p>
              <p>Los grupos definen el período, el día de corte y las horas por turno. Se dan de alta aquí en la siguiente entrega de Nómina.</p>
            </div>
          </mat-tab>
          <mat-tab label="Puestos">
            <div class="empty-state">
              <mat-icon fontSet="material-symbols-outlined">badge</mat-icon>
              <p>Sin puestos. Los puestos definen las tarifas que usa la prenómina.</p>
            </div>
          </mat-tab>
        </mat-tab-group>
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
  `,
})
export class PayrollClientDetail {
  /** Route param, read from the snapshot: the live query starts in the field initializer, before inputs are bound. */
  private readonly clientId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly id = () => this.clientId;
  readonly client = inject(ClientsRepository).detail('payroll', () => this.id());
}
