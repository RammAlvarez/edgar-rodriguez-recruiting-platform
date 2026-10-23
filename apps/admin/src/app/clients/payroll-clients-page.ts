import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { openSidePanel, Pager, type SortValue, UiPager } from '@platform/ui';
import type { ClientDetailDto, ClientListItemDto, PayrollGroupListItemDto } from '../data/api-types';
import { PayrollGroupsRepository } from '../data/payroll-repositories';
import { ClientsRepository } from '../data/repositories';
import { ClientPanel, type ClientPanelData } from './client-panel';

interface Project {
  readonly groups: readonly PayrollGroupListItemDto[];
  readonly positions: number;
  readonly employees: number;
}

const NO_PROJECT: Project = { groups: [], positions: 0, employees: 0 };

/**
 * Nómina → Clientes (nómina) (SPEC §5.6): clients with active projects, then all clients the Prenómina grant reaches.
 * A client has an active project when one of its groups has active employees (ARCHITECTURE §9.2 3f).
 */
@Component({
  selector: 'app-payroll-clients-page',
  imports: [MatSortModule, UiPager, MatTableModule, MatButton, MatIcon, MatProgressBar, MatFormField, MatLabel, MatInput, MatSuffix],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Clientes (nómina)</h1>
        <button matButton="filled" type="button" (click)="create()">
          <mat-icon fontSet="material-symbols-outlined">add_business</mat-icon>Nuevo cliente
        </button>
      </header>
      @if (clients.loading() || groups.loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (clients.error() ?? groups.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <h2 class="section">Clientes con proyectos activos</h2>
      @if (active().length === 0) {
        <div class="table-card empty-card">
          <p class="muted">Sin proyectos activos. Los clientes con grupos de nómina y empleados aparecerán aquí.</p>
        </div>
      } @else {
        <div class="table-card active-card">
          <table mat-table matSort [matSortActive]="activePager.sort().active" [matSortDirection]="activePager.sort().direction" (matSortChange)="activePager.sortChange($event)" [dataSource]="activePaged()">
            <ng-container matColumnDef="name">
              <th mat-header-cell mat-sort-header="name" *matHeaderCellDef>Cliente</th>
              <td mat-cell *matCellDef="let p"><strong>{{ p.client.shortName }}</strong></td>
            </ng-container>
            <ng-container matColumnDef="groups">
              <th mat-header-cell mat-sort-header="groups" *matHeaderCellDef>Grupos de nómina</th>
              <td mat-cell *matCellDef="let p">
                <span class="chips">
                  @for (g of p.groups; track g.id) {
                    <span class="pill">{{ g.name }}</span>
                  }
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="positions">
              <th mat-header-cell mat-sort-header="positions" *matHeaderCellDef class="num">Puestos</th>
              <td mat-cell *matCellDef="let p" class="num">{{ p.positions }}</td>
            </ng-container>
            <ng-container matColumnDef="employees">
              <th mat-header-cell mat-sort-header="employees" *matHeaderCellDef class="num">Empleados activos</th>
              <td mat-cell *matCellDef="let p" class="num">{{ p.employees }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="activeColumns"></tr>
            <tr mat-row *matRowDef="let p; columns: activeColumns" class="clickable" tabindex="0" (click)="go(p.client)" (keydown.enter)="go(p.client)"></tr>
          </table>
        <ui-pager [pager]="pager" [total]="rows().length" />
          <ui-pager [pager]="activePager" [total]="active().length" />
        </div>
      }
      <div class="section-bar">
        <h2 class="section">Todos los clientes</h2>
        <mat-form-field class="search" subscriptSizing="dynamic">
          <mat-label>Buscar cliente</mat-label>
          <input matInput [value]="search()" (input)="search.set($any($event.target).value)" />
          <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
        </mat-form-field>
      </div>
      <div class="table-card">
        <table mat-table matSort [matSortActive]="pager.sort().active" [matSortDirection]="pager.sort().direction" (matSortChange)="pager.sortChange($event)" [dataSource]="paged()">
          <ng-container matColumnDef="name">
            <th mat-header-cell mat-sort-header="name" *matHeaderCellDef>Cliente</th>
            <td mat-cell *matCellDef="let c"><strong>{{ c.shortName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="legal">
            <th mat-header-cell mat-sort-header="legal" *matHeaderCellDef>Razón social</th>
            <td mat-cell *matCellDef="let c">{{ c.legalName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="rfc">
            <th mat-header-cell mat-sort-header="rfc" *matHeaderCellDef>RFC</th>
            <td mat-cell *matCellDef="let c" class="mono">{{ c.rfc ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="groups">
            <th mat-header-cell mat-sort-header="groups" *matHeaderCellDef class="num">Grupos de nómina</th>
            <td mat-cell *matCellDef="let c" class="num">{{ project(c).groups.length }}</td>
          </ng-container>
          <ng-container matColumnDef="employees">
            <th mat-header-cell mat-sort-header="employees" *matHeaderCellDef class="num">Empleados activos</th>
            <td mat-cell *matCellDef="let c" class="num">{{ project(c).employees }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell mat-sort-header="status" *matHeaderCellDef>Estatus</th>
            <td mat-cell *matCellDef="let c">
              <span class="pill" [class.on]="c.isActive && project(c).employees > 0">{{ statusText(c) }}</span>
            </td>
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
    .active-card {
      margin-bottom: 24px;
    }
    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .pill.on {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
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
  readonly groups = inject(PayrollGroupsRepository).all();
  readonly search = signal('');
  readonly columns = ['name', 'legal', 'rfc', 'groups', 'employees', 'status'];
  readonly activeColumns = ['name', 'groups', 'positions', 'employees'];

  /** Per client: its groups, active puestos and active employees. */
  private readonly projects = computed(() => {
    const byClient = new Map<string, Project>();
    for (const g of this.groups.value() ?? []) {
      const p = byClient.get(g.clientId) ?? { groups: [], positions: 0, employees: 0 };
      byClient.set(g.clientId, { groups: [...p.groups, g], positions: p.positions + Number(g.positions), employees: p.employees + Number(g.activeEmployees) });
    }
    return byClient;
  });
  readonly active = computed(() =>
    (this.clients.value() ?? [])
      .filter((c) => c.isActive && (this.projects().get(c.id)?.employees ?? 0) > 0)
      .map((client) => ({ client, ...this.projects().get(client.id)! })),
  );
  readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase();
    return (this.clients.value() ?? []).filter(
      (c) => !q || c.shortName.toLowerCase().includes(q) || (c.legalName ?? '').toLowerCase().includes(q),
    );
  });

  readonly activeSortBy: Record<string, (p: { client: ClientListItemDto } & Project) => SortValue> = {
    name: (p) => p.client.shortName,
    groups: (p) => p.groups.map((g) => g.name).join(' '),
    positions: (p) => p.positions,
    employees: (p) => p.employees,
  };
  readonly sortBy: Record<string, (c: ClientListItemDto) => SortValue> = {
    name: (c) => c.shortName,
    legal: (c) => c.legalName,
    rfc: (c) => c.rfc,
    groups: (c) => this.project(c).groups.length,
    employees: (c) => this.project(c).employees,
    status: (c) => this.statusText(c),
  };
  readonly activePager = new Pager();
  readonly activePaged = computed(() => this.activePager.slice(this.active(), this.activeSortBy));
  readonly pager = new Pager();
  readonly paged = computed(() => this.pager.slice(this.rows(), this.sortBy));

  project(c: ClientListItemDto): Project {
    return this.projects().get(c.id) ?? NO_PROJECT;
  }

  /** «Activo» when the client is in the first table. */
  statusText(c: ClientListItemDto): string {
    if (!c.isActive) return 'Inactivo';
    return this.project(c).employees > 0 ? 'Activo' : 'Sin proyecto';
  }

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
