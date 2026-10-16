import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatCheckbox } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import type { AssignmentRole, OpeningDetailDto } from '../data/api-types';
import { OpeningsRepository, RecruitersRepository } from '../data/recruiting-repositories';

export interface AssignRecruiterData {
  readonly opening: OpeningDetailDto;
}

/** «Asignar reclutador» of the vacante detail (SPEC §4.4): the symmetric of «Asignar vacante» in the recruiter's detail. */
@Component({
  selector: 'app-assign-recruiter-panel',
  imports: [FormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatCheckbox, MatButtonToggleGroup, MatButtonToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Asignar reclutador</h2>
    <mat-dialog-content>
      <p class="muted">{{ data.opening.displayName }}</p>
      <mat-button-toggle-group [value]="role()" (change)="setRole($event.value)" aria-label="Rol" hideSingleSelectionIndicator>
        <mat-button-toggle value="support">Apoyo</mat-button-toggle>
        <mat-button-toggle value="lead">Líder</mat-button-toggle>
      </mat-button-toggle-group>
      @if (role() === 'lead' && data.opening.leadRecruiterName && selected().size > 0) {
        <p class="note">{{ data.opening.leadRecruiterName }} pasará a Apoyo.</p>
      }
      <div class="list">
        @for (r of recruiters(); track r.id) {
          <div class="row">
            <mat-checkbox [checked]="selected().has(r.id)" [disabled]="isAssigned(r.id)" (change)="toggle(r.id, $event.checked)">
              {{ r.displayName }}
            </mat-checkbox>
            <span class="muted">{{ assignedLabel(r.id) ?? r.openingsAssigned + ' vacantes' }}</span>
          </div>
        } @empty {
          <p class="muted">No hay reclutadores activos.</p>
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
    mat-button-toggle-group {
      margin: 8px 0;
    }
    .list {
      display: flex;
      flex-direction: column;
      margin-top: 8px;
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .note {
      margin: 4px 0;
      color: var(--mat-sys-tertiary);
    }
  `,
})
export class AssignRecruiterPanel {
  readonly data = inject<AssignRecruiterData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<AssignRecruiterPanel, boolean>);
  private readonly openings = inject(OpeningsRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly list = inject(RecruitersRepository).list();

  readonly role = signal<AssignmentRole>(this.data.opening.leadRecruiterId ? 'support' : 'lead');
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly busy = signal(false);
  readonly alert = signal<string | null>(null);
  readonly recruiters = computed(() => (this.list.value() ?? []).filter((r) => r.isActive));

  isAssigned(id: string): boolean {
    return this.data.opening.recruiters.some((r) => r.userId === id);
  }

  assignedLabel(id: string): string | null {
    const r = this.data.opening.recruiters.find((x) => x.userId === id);
    return r ? (r.isLead ? 'Líder' : 'Apoyo') : null;
  }

  setRole(role: AssignmentRole): void {
    this.role.set(role);
    // A vacante has one líder.
    if (role === 'lead' && this.selected().size > 1) this.selected.set(new Set([...this.selected()].slice(0, 1)));
  }

  toggle(id: string, on: boolean): void {
    const next = this.role() === 'lead' ? new Set<string>() : new Set(this.selected());
    if (on) next.add(id);
    else next.delete(id);
    this.selected.set(next);
  }

  async save(): Promise<void> {
    this.busy.set(true);
    this.alert.set(null);
    try {
      await this.openings.assign(this.data.opening.id, [...this.selected()], this.role());
      this.snack.open(this.selected().size === 1 ? 'Reclutador asignado' : `${this.selected().size} reclutadores asignados`, undefined, {
        duration: 4000,
      });
      this.ref.close(true);
    } catch (e) {
      this.alert.set(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.');
    } finally {
      this.busy.set(false);
    }
  }
}
