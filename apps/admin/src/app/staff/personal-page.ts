import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel, MatPrefix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { type DateKey, formatDateKey } from '@platform/shared';
import { openSidePanel } from '@platform/ui';
import { DateKeyPipe } from '@platform/ui/pipes';
import type { EmployeeListItemDto, EmployeeStatus, RegistrationBank } from '../data/api-types';
import { EMPLOYEE_STATUS, PERIOD_STATUS_LABEL, REGISTRATION_BANK } from '../data/payroll-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { EmployeePanel, type EmployeePanelData } from './employee-panel';

/** Documents below this are «incompletos» (the required ones of the «Empleado» template). */
const COMPLETE = 100;

/**
 * Nómina → Personal (SPEC §5.1): filters cliente, puesto, estatus and documentos incompletos; the row's only quick action is
 * «Solicitar tarjeta» — shown when the employee has no account and no request (a pending one shows as a pill instead).
 */
@Component({
  selector: 'app-personal-page',
  imports: [MatButton, MatCheckbox, MatFormField, MatLabel, MatPrefix, MatIcon, MatInput, MatMenu, MatMenuItem, MatMenuTrigger, MatProgressBar, MatSelect, MatOption, DateKeyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Personal</h1>
        <button matButton="filled" type="button" (click)="alta()"><mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Alta de empleado</button>
      </header>
      <div class="filter-bar">
        <mat-form-field subscriptSizing="dynamic" class="search">
          <mat-label>Buscar</mat-label>
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <input matInput [value]="search()" (input)="search.set($any($event.target).value)" autocomplete="off" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Cliente</mat-label>
          <mat-select [value]="clientId()" (valueChange)="clientId.set($event); position.set(null)">
            <mat-option [value]="null">Todos</mat-option>
            @for (c of clients(); track c.id) {
              <mat-option [value]="c.id">{{ c.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Puesto</mat-label>
          <mat-select [value]="position()" (valueChange)="position.set($event)">
            <mat-option [value]="null">Todos</mat-option>
            @for (p of positionNames(); track p) {
              <mat-option [value]="p">{{ p }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Estatus</mat-label>
          <mat-select [value]="statusFilter()" (valueChange)="statusFilter.set($event)">
            <mat-option [value]="null">Todos</mat-option>
            @for (s of statuses; track s) {
              <mat-option [value]="s">{{ status[s].label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-checkbox [checked]="docsIncomplete()" (change)="docsIncomplete.set($event.checked)">Documentos incompletos</mat-checkbox>
      </div>
      @if (list.loading() && !list.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (list.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="table-card">
        <table>
          <thead>
            <tr><th>Nombre</th><th>Cliente</th><th>Grupo de nómina</th><th>Puesto</th><th>Turno</th><th>Ingreso</th><th>Estatus</th><th>Documentos</th><th></th></tr>
          </thead>
          <tbody>
            @for (e of rows(); track e.id) {
              <tr class="clickable" (click)="open(e)">
                <td class="name">{{ e.fullName }}</td>
                <td>{{ e.clientName }}</td>
                <td>{{ e.groupName }}</td>
                <td>{{ e.positionName }}</td>
                <td>{{ e.shift }}</td>
                <td class="nowrap">{{ e.hiredAt | dateKey }}</td>
                <td>
                  <span class="pill" [class]="status[e.status].tone">{{ status[e.status].label }}</span>
                  @if (e.period; as p) {
                    <div class="muted dates">{{ e.status === 'active' ? label[p.status] + ' ' : '' }}{{ range(p.startDate, p.endDate) }}</div>
                  }
                </td>
                <td [class.incomplete]="+e.documentsCompleteness < 100">
                  <div class="docs">
                    <mat-progress-bar mode="determinate" [value]="+percent(e)" aria-label="Documentos" />
                    <span>{{ percent(e) }} %</span>
                  </div>
                </td>
                <td class="quick" (click)="$event.stopPropagation()">
                  @if (e.cardRequest; as r) {
                    <span class="pill">Tarjeta solicitada · {{ bank[r.bank] }}</span>
                  } @else if (!e.hasAccount && e.status !== 'terminated') {
                    <button matButton type="button" [matMenuTriggerFor]="banks">Solicitar tarjeta</button>
                    <mat-menu #banks="matMenu">
                      @for (b of cardBanks; track b) {
                        <button mat-menu-item type="button" (click)="requestCard(e, b)">{{ bank[b] }}</button>
                      }
                    </mat-menu>
                  }
                </td>
              </tr>
            } @empty {
              @if (list.value()) {
                <tr class="empty-row"><td colspan="9">Sin empleados con estos filtros.</td></tr>
              }
            }
          </tbody>
        </table>
      </div>
      <p class="muted count">{{ rows().length }} empleados@if (statusFilter() !== 'terminated') { · las bajas se ven con Estatus «Baja» }</p>
    </div>
  `,
  styles: `
    table {
      border-collapse: collapse;
    }
    th,
    td {
      padding: 8px 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      text-align: left;
      vertical-align: top;
    }
    th {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .name {
      font-weight: 500;
    }
    .docs {
      display: flex;
      align-items: center;
      gap: 8px;
      mat-progress-bar {
        width: 72px;
      }
      span {
        min-width: 3ch;
        text-align: right;
        font-variant-numeric: tabular-nums;
      }
    }
    .nowrap {
      white-space: nowrap;
    }
    .dates {
      margin-top: 2px;
      font: var(--mat-sys-body-small);
      white-space: nowrap;
    }
    .incomplete {
      color: var(--mat-sys-error);
    }
    .quick {
      white-space: nowrap;
      text-align: right;
    }
    .count {
      margin-top: 8px;
      font: var(--mat-sys-body-small);
    }
  `,
})
export class PersonalPage {
  private readonly repo = inject(EmployeesRepository);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly options = this.repo.options();

  readonly search = signal('');
  readonly clientId = signal<string | null>(null);
  readonly position = signal<string | null>(null);
  readonly statusFilter = signal<EmployeeStatus | null>(null);
  readonly docsIncomplete = signal(false);

  readonly list = this.repo.list(() => ({ clientId: this.clientId(), includeTerminated: this.statusFilter() === 'terminated' }));
  readonly status = EMPLOYEE_STATUS;
  readonly label = PERIOD_STATUS_LABEL;
  readonly bank = REGISTRATION_BANK;
  readonly cardBanks: RegistrationBank[] = ['banorte', 'banregio'];
  readonly statuses = Object.keys(EMPLOYEE_STATUS) as EmployeeStatus[];

  readonly clients = computed(() => {
    const seen = new Map<string, string>();
    for (const g of this.options.value()?.groups ?? []) seen.set(g.clientId, g.clientName);
    return [...seen].map(([id, name]) => ({ id, name }));
  });
  readonly positionNames = computed(() => [
    ...new Set((this.options.value()?.groups ?? []).filter((g) => !this.clientId() || g.clientId === this.clientId()).flatMap((g) => g.positions.map((p) => p.name))),
  ].sort());

  readonly rows = computed(() => {
    const text = this.search().trim().toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
    return (this.list.value() ?? []).filter(
      (e) =>
        (!this.position() || e.positionName === this.position()) &&
        (!this.statusFilter() || e.status === this.statusFilter()) &&
        (!this.docsIncomplete() || Number(e.documentsCompleteness) < COMPLETE) &&
        (!text || e.fullName.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').includes(text)),
    );
  });

  constructor() {
    // The cliente filter and «Baja» change what the API returns; the rest filters in the browser.
    let first = true;
    effect(() => {
      this.clientId();
      this.statusFilter();
      if (first) {
        first = false;
        return;
      }
      untracked(() => this.list.reload());
    });
  }

  range(start: string, end: string | null): string {
    return end ? `${formatDateKey(start as DateKey)} – ${formatDateKey(end as DateKey)}` : `desde ${formatDateKey(start as DateKey)}`;
  }

  percent(e: EmployeeListItemDto): string {
    return String(Math.trunc(Number(e.documentsCompleteness)));
  }

  open(e: EmployeeListItemDto): void {
    void this.router.navigate(['/personal', e.id]);
  }

  alta(): void {
    openSidePanel<EmployeePanel, EmployeePanelData>(this.dialog, EmployeePanel, { mode: 'alta' });
  }

  async requestCard(e: EmployeeListItemDto, bank: RegistrationBank): Promise<void> {
    try {
      await this.repo.requestCard(e.id, bank);
      this.snack.open(`Tarjeta ${REGISTRATION_BANK[bank]} solicitada para ${e.fullName}`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(err instanceof ApiError ? err.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    }
  }
}
