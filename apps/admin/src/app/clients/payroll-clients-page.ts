import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { openSidePanel } from '@platform/ui';
import type { ClientDetailDto, ClientListItemDto } from '../data/api-types';
import { ClientsRepository } from '../data/repositories';
import { ClientPanel, type ClientPanelData } from './client-panel';

/**
 * Nómina → Clientes (nómina) (SPEC §5.6): clients with active projects, then all clients the Prenómina grant reaches.
 * Payroll groups, positions and employees arrive in S7; until then every client is «Sin proyecto».
 */
@Component({
  selector: 'app-payroll-clients-page',
  imports: [MatTableModule, MatButton, MatIcon, MatProgressBar, MatFormField, MatLabel, MatInput, MatSuffix],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Clientes (nómina)</h1>
        <button matButton="filled" type="button" (click)="create()">
          <mat-icon fontSet="material-symbols-outlined">add_business</mat-icon>Nuevo cliente
        </button>
      </header>
      @if (clients.loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (clients.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <h2 class="section">Clientes con proyectos activos</h2>
      <div class="table-card empty-card">
        <p class="muted">Sin proyectos activos. Los clientes con grupos de nómina y empleados aparecerán aquí.</p>
      </div>
      <div class="section-bar">
        <h2 class="section">Todos los clientes</h2>
        <mat-form-field class="search" subscriptSizing="dynamic">
          <mat-label>Buscar cliente</mat-label>
          <input matInput [value]="search()" (input)="search.set($any($event.target).value)" />
          <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
        </mat-form-field>
      </div>
      <div class="table-card">
        <table mat-table [dataSource]="rows()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Cliente</th>
            <td mat-cell *matCellDef="let c"><strong>{{ c.shortName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="legal">
            <th mat-header-cell *matHeaderCellDef>Razón social</th>
            <td mat-cell *matCellDef="let c">{{ c.legalName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="rfc">
            <th mat-header-cell *matHeaderCellDef>RFC</th>
            <td mat-cell *matCellDef="let c" class="mono">{{ c.rfc ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="groups">
            <th mat-header-cell *matHeaderCellDef class="num">Grupos de nómina</th>
            <td mat-cell *matCellDef="let c" class="num">0</td>
          </ng-container>
          <ng-container matColumnDef="employees">
            <th mat-header-cell *matHeaderCellDef class="num">Empleados activos</th>
            <td mat-cell *matCellDef="let c" class="num">0</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Estatus</th>
            <td mat-cell *matCellDef="let c"><span class="pill">{{ c.isActive ? 'Sin proyecto' : 'Inactivo' }}</span></td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let c; columns: columns" class="clickable" tabindex="0" (click)="go(c)" (keydown.enter)="go(c)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">Sin clientes que coincidan con la búsqueda.</td>
          </tr>
        </table>
      </div>
    </div>
  `,
  styles: `
    .section {
      margin: 0 0 8px;
      font: var(--mat-sys-title-medium);
    }
    .empty-card {
      margin-bottom: 24px;
      padding: 16px;
      p {
        margin: 0;
      }
    }
    .section-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 16px;
      margin-bottom: 8px;
    }
    .search {
      width: 280px;
      max-width: 100%;
    }
  `,
})
export class PayrollClientsPage {
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly clients = inject(ClientsRepository).list('payroll');
  readonly search = signal('');
  readonly columns = ['name', 'legal', 'rfc', 'groups', 'employees', 'status'];
  readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase();
    return (this.clients.value() ?? []).filter(
      (c) => !q || c.shortName.toLowerCase().includes(q) || (c.legalName ?? '').toLowerCase().includes(q),
    );
  });

  go(c: Pick<ClientListItemDto, 'id'>): void {
    void this.router.navigate(['/clientes-nomina', c.id]);
  }

  /** Creates a payroll client and opens its detail on «Grupos de nómina» (ARCHITECTURE §9.2). */
  create(): void {
    openSidePanel<ClientPanel, ClientPanelData, ClientDetailDto>(this.dialog, ClientPanel, { module: 'payroll' })
      .afterClosed()
      .subscribe((created) => {
        if (created && typeof created === 'object') this.go(created);
      });
  }
}
