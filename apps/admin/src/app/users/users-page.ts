import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltip } from '@angular/material/tooltip';
import { openSidePanel, Pager, Session, type SortValue, TimestampPipe, UiPager } from '@platform/ui';
import type { UserDto } from '../data/api-types';
import { ROLE_LABELS } from '../data/labels';
import { ClientsRepository, UsersRepository } from '../data/repositories';
import { clientColumn } from './grants';
import { UserPanel, type UserPanelData } from './user-panel';

/** Administración → Usuarios (SPEC §6): table; a row opens «Editar usuario», the header button «Invitar usuario». */
@Component({
  selector: 'app-users-page',
  imports: [MatSortModule, UiPager, MatTableModule, MatButton, MatIcon, MatProgressBar, MatTooltip, TimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>Usuarios</h1>
        <button matButton="filled" type="button" (click)="open(null)">
          <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Invitar usuario
        </button>
      </header>
      @if (users.loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (users.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="table-card">
        <table mat-table matSort [matSortActive]="pager.sort().active" [matSortDirection]="pager.sort().direction" (matSortChange)="pager.sortChange($event)" [dataSource]="paged()">
          <ng-container matColumnDef="name">
            <th mat-header-cell mat-sort-header="name" *matHeaderCellDef>Nombre</th>
            <td mat-cell *matCellDef="let u">
              <strong>{{ u.displayName }}</strong>
              @if (u.id === me()?.id) {
                <span class="muted"> · tú</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell mat-sort-header="email" *matHeaderCellDef>Correo</th>
            <td mat-cell *matCellDef="let u">{{ u.email }}</td>
          </ng-container>
          <ng-container matColumnDef="role">
            <th mat-header-cell mat-sort-header="role" *matHeaderCellDef>Rol</th>
            <td mat-cell *matCellDef="let u">{{ roleLabel(u) }}</td>
          </ng-container>
          <ng-container matColumnDef="client">
            <th mat-header-cell mat-sort-header="client" *matHeaderCellDef>Cliente</th>
            <td mat-cell *matCellDef="let u">{{ clientOf(u) }}</td>
          </ng-container>
          <ng-container matColumnDef="mfa">
            <th mat-header-cell mat-sort-header="mfa" *matHeaderCellDef>MFA</th>
            <td mat-cell *matCellDef="let u">
              <mat-icon fontSet="material-symbols-outlined" [matTooltip]="u.mfaRequired ? 'MFA requerido' : 'MFA opcional'">
                {{ u.mfaRequired ? 'verified_user' : 'remove_moderator' }}
              </mat-icon>
            </td>
          </ng-container>
          <ng-container matColumnDef="lastLogin">
            <th mat-header-cell mat-sort-header="lastLogin" *matHeaderCellDef>Último acceso</th>
            <td mat-cell *matCellDef="let u">{{ u.lastLoginAt ? (u.lastLoginAt | timestamp) : 'Invitación pendiente' }}</td>
          </ng-container>
          <ng-container matColumnDef="active">
            <th mat-header-cell mat-sort-header="active" *matHeaderCellDef>Activo</th>
            <td mat-cell *matCellDef="let u">
              <span class="pill" [class.positive]="u.isActive" [class.negative]="!u.isActive">{{ u.isActive ? 'Activo' : 'Inactivo' }}</span>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let u; columns: columns" class="clickable" tabindex="0" (click)="open(u)" (keydown.enter)="open(u)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">Sin usuarios. Los usuarios invitados aparecerán aquí.</td>
          </tr>
        </table>
        <ui-pager [pager]="pager" [total]="rows().length" />
      </div>
    </div>
  `,
})
export class UsersPage {
  private readonly dialog = inject(MatDialog);
  readonly users = inject(UsersRepository).list();
  private readonly clients = inject(ClientsRepository).options();
  readonly me = inject(Session).me;

  readonly columns = ['name', 'email', 'role', 'client', 'mfa', 'lastLogin', 'active'];
  readonly rows = computed(() => this.users.value() ?? []);
  readonly sortBy: Record<string, (u: UserDto) => SortValue> = {
    name: (u) => u.displayName,
    email: (u) => u.email,
    role: (u) => this.roleLabel(u),
    client: (u) => this.clientOf(u),
    mfa: (u) => u.mfaRequired,
    lastLogin: (u) => u.lastLoginAt,
    active: (u) => u.isActive,
  };
  readonly pager = new Pager();
  readonly paged = computed(() => this.pager.slice(this.rows(), this.sortBy));

  clientOf(u: UserDto): string {
    return clientColumn(u, (id) => this.clients.value()?.find((c) => c.id === id)?.shortName ?? '—');
  }

  roleLabel(u: UserDto): string {
    return ROLE_LABELS[u.role];
  }

  open(user: UserDto | null): void {
    openSidePanel<UserPanel, UserPanelData>(this.dialog, UserPanel, {
      user,
      clients: this.clients.value() ?? [],
      myId: this.me()?.id ?? '',
    });
  }
}
