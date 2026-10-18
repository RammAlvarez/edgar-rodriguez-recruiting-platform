import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatCheckbox } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatFormField, MatLabel, MatPrefix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import type { AssignmentRole, RecruiterDetailDto } from '../data/api-types';
import { OpeningsRepository, RecruitersRepository } from '../data/recruiting-repositories';

export interface AssignOpeningData {
  readonly recruiter: RecruiterDetailDto;
}

/**
 * «Asignar vacante a Mónica Soto» (SPEC §4.4, ARCHITECTURE §9.10): open / in-process vacantes not yet hers, with the current
 * líder and días abierta; role Apoyo by default, Líder when the vacante has none; a note when a líder will become apoyo.
 */
@Component({
  selector: 'app-assign-opening-panel',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatPrefix, MatInput, MatIcon, MatCheckbox,
    MatButtonToggleGroup, MatButtonToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Asignar vacante a {{ data.recruiter.recruiter.displayName }}</h2>
    <mat-dialog-content>
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-label>Buscar vacante</mat-label>
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [value]="search()" (input)="search.set($any($event.target).value)" autocomplete="off" />
      </mat-form-field>
      <mat-button-toggle-group [value]="role()" (change)="role.set($event.value)" aria-label="Rol" hideSingleSelectionIndicator>
        <mat-button-toggle value="support">Apoyo</mat-button-toggle>
        <mat-button-toggle value="lead">Líder</mat-button-toggle>
      </mat-button-toggle-group>
      <div class="list">
        @for (o of rows(); track o.id) {
          <div class="row">
            <mat-checkbox [checked]="selected().has(o.id)" (change)="toggle(o.id, $event.checked)">{{ o.displayName }}</mat-checkbox>
            <span class="muted">{{ o.leadRecruiterName ?? 'Sin líder' }} · {{ o.daysOpen }} d</span>
            @if (role() === 'lead' && selected().has(o.id) && o.leadRecruiterName) {
              <span class="note">{{ o.leadRecruiterName }} pasará a Apoyo</span>
            }
          </div>
        } @empty {
          <p class="muted">No hay vacantes abiertas o en proceso pendientes de asignar.</p>
        }
      </div>
      @if (alert()) {
        <p class="form-alert" role="alert">{{ alert() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="button" [disabled]="busy() || selected().size === 0" (click)="save()">
        Asignar ({{ selected().size }})
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .search {
      width: 100%;
      margin-top: 8px;
    }
    mat-button-toggle-group {
      margin: 12px 0 8px;
    }
    .list {
      display: flex;
      flex-direction: column;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      column-gap: 8px;
      .muted {
        margin-left: auto;
      }
    }
    .note {
      flex-basis: 100%;
      margin: -4px 0 4px 40px;
      color: var(--mat-sys-tertiary);
    }
  `,
})
export class AssignOpeningPanel {
  readonly data = inject<AssignOpeningData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<AssignOpeningPanel, boolean>);
  private readonly recruiters = inject(RecruitersRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly openings = inject(OpeningsRepository).list();

  readonly search = signal('');
  readonly role = signal<AssignmentRole>('support');
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly busy = signal(false);
  readonly alert = signal<string | null>(null);

  private readonly assigned = new Set(this.data.recruiter.assignments.map((a) => a.openingId));
  readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase();
    return (this.openings.value() ?? []).filter(
      (o) =>
        (o.status === 'open' || o.status === 'in_progress') &&
        !this.assigned.has(o.id) &&
        (!q || o.displayName.toLowerCase().includes(q)),
    );
  });

  toggle(id: string, on: boolean): void {
    const next = new Set(this.selected());
    if (on) next.add(id);
    else next.delete(id);
    this.selected.set(next);
    // A vacante without líder defaults to Líder (ARCHITECTURE §9.10).
    if (on && next.size === 1 && !this.rows().find((o) => o.id === id)?.leadRecruiterName) this.role.set('lead');
  }

  async save(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.alert.set(null);
    try {
      const n = this.selected().size;
      await this.recruiters.assignOpenings(this.data.recruiter.recruiter.id, [...this.selected()], this.role());
      this.snack.open(n === 1 ? '1 vacante asignada' : `${n} vacantes asignadas`, undefined, { duration: 4000 });
      this.ref.close(true);
    } catch (e) {
      this.alert.set(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.');
    } finally {
      this.busy.set(false);
    }
  }
}
