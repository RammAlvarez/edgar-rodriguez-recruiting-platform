import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { openSidePanel } from '@platform/ui';
import type { ClientDetailDto, ClientListItemDto, OpeningListItemDto } from '../data/api-types';
import { OpeningsRepository } from '../data/recruiting-repositories';
import { ClientsRepository } from '../data/repositories';
import { ClientPanel, type ClientPanelData } from './client-panel';

/** Reclutamiento → Clientes (SPEC §4.5): the clients used by recruiting, with their vacantes abiertas, cubiertas and días promedio. */
@Component({
  selector: 'app-clients-page',
  imports: [MatTableModule, MatButton, MatIcon, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Clientes</h1>
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
          <ng-container matColumnDef="zone">
            <th mat-header-cell *matHeaderCellDef>Zona</th>
            <td mat-cell *matCellDef="let c">{{ c.zone ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="contact">
            <th mat-header-cell *matHeaderCellDef>Contacto principal</th>
            <td mat-cell *matCellDef="let c">{{ c.primaryContact?.fullName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="open">
            <th mat-header-cell *matHeaderCellDef class="num">Vacantes abiertas</th>
            <td mat-cell *matCellDef="let c" class="num">{{ stats(c).open }}</td>
          </ng-container>
          <ng-container matColumnDef="filled">
            <th mat-header-cell *matHeaderCellDef class="num">Cubiertas</th>
            <td mat-cell *matCellDef="let c" class="num">{{ stats(c).filled }}</td>
          </ng-container>
          <ng-container matColumnDef="avgDays">
            <th mat-header-cell *matHeaderCellDef class="num">Días promedio</th>
            <td mat-cell *matCellDef="let c" class="num">{{ stats(c).avgDays ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Estatus</th>
            <td mat-cell *matCellDef="let c">
              <span class="pill" [class.positive]="c.isActive">{{ c.isActive ? 'Activo' : 'Inactivo' }}</span>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let c; columns: columns" class="clickable" tabindex="0" (click)="go(c)" (keydown.enter)="go(c)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">Sin clientes de reclutamiento. Los clientes que crees aquí aparecerán en esta tabla.</td>
          </tr>
        </table>
      </div>
    </div>
  `,
})
export class ClientsPage {
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly clients = inject(ClientsRepository).list('recruiting');
  readonly columns = ['name', 'legal', 'zone', 'contact', 'open', 'filled', 'avgDays', 'status'];
  private readonly openings = inject(OpeningsRepository).list();
  /** Per client: open / in-process vacantes, filled ones, and the average días abierta of the filled ones (truncated). */
  private readonly byClient = computed(() => {
    const map = new Map<string, { open: number; filled: number; avgDays: number | null }>();
    const groups = new Map<string, OpeningListItemDto[]>();
    for (const o of this.openings.value() ?? []) groups.set(o.clientId, [...(groups.get(o.clientId) ?? []), o]);
    for (const [id, list] of groups) {
      const filled = list.filter((o) => o.status === 'filled');
      map.set(id, {
        open: list.filter((o) => o.status === 'open' || o.status === 'in_progress').length,
        filled: filled.length,
        avgDays: filled.length ? Math.trunc(filled.reduce((s, o) => s + Number(o.daysOpen), 0) / filled.length) : null,
      });
    }
    return map;
  });

  stats(c: ClientListItemDto) {
    return this.byClient().get(c.id) ?? { open: 0, filled: 0, avgDays: null };
  }
  readonly rows = computed(() => (this.clients.value() ?? []).filter((c) => c.usesRecruiting));

  go(c: ClientListItemDto): void {
    void this.router.navigate(['/clientes', c.id]);
  }

  create(): void {
    openSidePanel<ClientPanel, ClientPanelData, ClientDetailDto>(this.dialog, ClientPanel, { module: 'recruiting' })
      .afterClosed()
      .subscribe((created) => {
        if (created && typeof created === 'object') this.go({ id: created.id } as ClientListItemDto);
      });
  }
}
