import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { openSidePanel, Pager, type SortValue, UiPager } from '@platform/ui';
import type { OpeningDetailDto, OpeningListItemDto } from '../data/api-types';
import { OPENING_STATUS, PRIORITY, type OpeningStatus } from '../data/recruiting-labels';
import { OpeningsRepository } from '../data/recruiting-repositories';
import { OpeningPanel, type OpeningPanelData } from './opening-panel';

/**
 * Reclutamiento → Vacantes (SPEC §4.1, ARCHITECTURE §9 2a) and the recruiter's «Mis vacantes» (route data `workspace`):
 * the same table; the API already limits a Reclutador — or cover mode — to her vacantes.
 */
@Component({
  selector: 'app-openings-page',
  imports: [MatSortModule, UiPager, MatTableModule, MatButton, MatIcon, MatProgressBar, MatFormField, MatLabel, MatSelect, MatOption],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>{{ workspace ? 'Mis vacantes' : 'Vacantes' }}</h1>
        @if (!workspace) {
          <button matButton="filled" type="button" (click)="create()">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nueva vacante
          </button>
        }
      </header>
      <div class="filter-bar">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Cliente</mat-label>
          <mat-select [value]="client()" (valueChange)="client.set($event)">
            <mat-option [value]="null">Todos</mat-option>
            @for (c of clientOptions(); track c) {
              <mat-option [value]="c">{{ c }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Estatus</mat-label>
          <mat-select [value]="status()" (valueChange)="status.set($event)">
            <mat-option [value]="null">Todos</mat-option>
            @for (s of statuses; track s.value) {
              <mat-option [value]="s.value">{{ s.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (!workspace) {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Reclutador</mat-label>
            <mat-select [value]="recruiter()" (valueChange)="recruiter.set($event)">
              <mat-option [value]="null">Todos</mat-option>
              @for (r of recruiterOptions(); track r) {
                <mat-option [value]="r">{{ r }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </div>
      @if (openings.loading() && !openings.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (openings.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="table-card">
        <table mat-table matSort [matSortActive]="pager.sort().active" [matSortDirection]="pager.sort().direction" (matSortChange)="pager.sortChange($event)" [dataSource]="paged()">
          <ng-container matColumnDef="client">
            <th mat-header-cell mat-sort-header="client" *matHeaderCellDef>Cliente</th>
            <td mat-cell *matCellDef="let o"><strong>{{ o.clientName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="position">
            <th mat-header-cell mat-sort-header="position" *matHeaderCellDef>Puesto</th>
            <td mat-cell *matCellDef="let o">{{ positionOf(o) }}</td>
          </ng-container>
          <ng-container matColumnDef="zone">
            <th mat-header-cell mat-sort-header="zone" *matHeaderCellDef>Zona</th>
            <td mat-cell *matCellDef="let o">{{ o.zoneName ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="requested">
            <th mat-header-cell mat-sort-header="requested" *matHeaderCellDef class="num">Solicitadas</th>
            <td mat-cell *matCellDef="let o" class="num">{{ o.headcountRequested }}</td>
          </ng-container>
          <ng-container matColumnDef="hired">
            <th mat-header-cell mat-sort-header="hired" *matHeaderCellDef class="num">Contratados</th>
            <td mat-cell *matCellDef="let o" class="num">{{ o.hired }}</td>
          </ng-container>
          <ng-container matColumnDef="priority">
            <th mat-header-cell mat-sort-header="priority" *matHeaderCellDef>Prioridad</th>
            <td mat-cell *matCellDef="let o">{{ priorityOf(o) }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell mat-sort-header="status" *matHeaderCellDef>Estatus</th>
            <td mat-cell *matCellDef="let o">
              <span class="pill" [class]="statusOf(o).tone">{{ statusOf(o).label }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="days">
            <th mat-header-cell mat-sort-header="days" *matHeaderCellDef class="num">Días abierta</th>
            <td mat-cell *matCellDef="let o" class="num">{{ o.daysOpen }}</td>
          </ng-container>
          <ng-container matColumnDef="lead">
            <th mat-header-cell mat-sort-header="lead" *matHeaderCellDef>Reclutador líder</th>
            <td mat-cell *matCellDef="let o">{{ o.leadRecruiterName ?? 'Sin líder' }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let o; columns: columns" class="clickable" tabindex="0" (click)="go(o)" (keydown.enter)="go(o)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">
              {{ workspace ? 'No tienes vacantes asignadas.' : 'Sin vacantes con estos filtros.' }}
            </td>
          </tr>
        </table>
        <ui-pager [pager]="pager" [total]="rows().length" />
      </div>
    </div>
  `,
})
export class OpeningsPage {
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly workspace = inject(ActivatedRoute).snapshot.data['workspace'] === true;
  readonly openings = inject(OpeningsRepository).list();
  readonly statuses = (Object.keys(OPENING_STATUS) as OpeningStatus[]).map((value) => ({ value, label: OPENING_STATUS[value].label }));

  readonly client = signal<string | null>(null);
  readonly status = signal<OpeningStatus | null>(null);
  readonly recruiter = signal<string | null>(null);

  readonly columns = this.workspace
    ? ['client', 'position', 'zone', 'requested', 'hired', 'priority', 'status', 'days']
    : ['client', 'position', 'zone', 'requested', 'hired', 'priority', 'status', 'days', 'lead'];
  readonly clientOptions = computed(() => [...new Set((this.openings.value() ?? []).map((o) => o.clientName))].sort());
  readonly recruiterOptions = computed(() =>
    [...new Set((this.openings.value() ?? []).map((o) => o.leadRecruiterName).filter((n): n is string => !!n))].sort(),
  );
  readonly rows = computed(() =>
    (this.openings.value() ?? []).filter(
      (o) =>
        (!this.client() || o.clientName === this.client()) &&
        (!this.status() || o.status === this.status()) &&
        (!this.recruiter() || o.leadRecruiterName === this.recruiter()),
    ),
  );

  readonly sortBy: Record<string, (o: OpeningListItemDto) => SortValue> = {
    client: (o) => o.clientName,
    position: (o) => this.positionOf(o),
    zone: (o) => o.zoneName,
    requested: (o) => o.headcountRequested,
    hired: (o) => o.hired,
    priority: (o) => o.priority === 'high' ? 0 : o.priority === 'medium' ? 1 : 2,
    status: (o) => this.statusOf(o).label,
    days: (o) => o.daysOpen,
    lead: (o) => o.leadRecruiterName,
  };
  readonly pager = new Pager();
  readonly paged = computed(() => this.pager.slice(this.rows(), this.sortBy));

  /** The puesto as the display name shows it: everything after "Cliente – " (keeps the date suffix of duplicates). */
  positionOf(o: OpeningListItemDto): string {
    return o.displayName.slice(o.clientName.length + 3);
  }

  priorityOf(o: OpeningListItemDto): string {
    return PRIORITY[o.priority];
  }

  statusOf(o: OpeningListItemDto) {
    return OPENING_STATUS[o.status];
  }

  go(o: { id: string }): void {
    void this.router.navigate([this.workspace ? '/mis-vacantes' : '/vacantes', o.id]);
  }

  create(): void {
    openSidePanel<OpeningPanel, OpeningPanelData, OpeningDetailDto>(this.dialog, OpeningPanel, {})
      .afterClosed()
      .subscribe((created) => {
        if (created) this.go(created);
      });
  }
}
