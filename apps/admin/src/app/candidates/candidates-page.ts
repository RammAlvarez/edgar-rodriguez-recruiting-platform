import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel, MatPrefix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { MoneyPipe, openSidePanel } from '@platform/ui';
import type { CandidateDetailDto, CandidateListItemDto } from '../data/api-types';
import { SOURCE, STAGE, STAGES, type CandidateStage } from '../data/recruiting-labels';
import { CandidatesRepository } from '../data/recruiting-repositories';
import { CandidatePanel, type CandidatePanelData } from './candidate-panel';

/**
 * Reclutamiento → Candidatos (SPEC §4.2, ARCHITECTURE §9 2b) and «Mis candidatos» (route data `workspace`): one row per
 * application, the vacante as "Cliente – Puesto"; the API limits a Reclutador — or cover mode — to her vacantes.
 */
@Component({
  selector: 'app-candidates-page',
  imports: [MatTableModule, MatButton, MatIcon, MatProgressBar, MatFormField, MatLabel, MatPrefix, MatInput, MatSelect, MatOption, MoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <h1>{{ workspace ? 'Mis candidatos' : 'Candidatos' }}</h1>
        <button matButton="filled" type="button" (click)="create()">
          <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Nuevo candidato
        </button>
      </header>
      <div class="filter-bar">
        <mat-form-field subscriptSizing="dynamic" class="search">
          <mat-label>Buscar por nombre o teléfono</mat-label>
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <input matInput [value]="search()" (input)="search.set($any($event.target).value)" autocomplete="off" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Vacante</mat-label>
          <mat-select [value]="opening()" (valueChange)="opening.set($event)">
            <mat-option [value]="null">Todas</mat-option>
            @for (o of openingOptions(); track o) {
              <mat-option [value]="o">{{ o }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Etapa</mat-label>
          <mat-select [value]="stage()" (valueChange)="stage.set($event)">
            <mat-option [value]="null">Todas</mat-option>
            @for (s of stages; track s) {
              <mat-option [value]="s">{{ stageOf(s).label }}</mat-option>
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
      @if (candidates.loading() && !candidates.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (candidates.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      <div class="table-card">
        <table mat-table [dataSource]="rows()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Nombre</th>
            <td mat-cell *matCellDef="let c"><strong>{{ c.fullName }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="phone">
            <th mat-header-cell *matHeaderCellDef>Teléfono</th>
            <td mat-cell *matCellDef="let c">{{ c.mobilePhone ?? '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="opening">
            <th mat-header-cell *matHeaderCellDef>Vacante</th>
            <td mat-cell *matCellDef="let c">{{ c.openingName }}</td>
          </ng-container>
          <ng-container matColumnDef="recruiter">
            <th mat-header-cell *matHeaderCellDef>Reclutador</th>
            <td mat-cell *matCellDef="let c">{{ c.recruiterName ?? 'Sin reclutador' }}</td>
          </ng-container>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Fuente</th>
            <td mat-cell *matCellDef="let c">{{ sourceOf(c) }}</td>
          </ng-container>
          <ng-container matColumnDef="stage">
            <th mat-header-cell *matHeaderCellDef>Etapa</th>
            <td mat-cell *matCellDef="let c"><span class="pill" [class]="stageOf(c.stage).tone">{{ stageOf(c.stage).label }}</span></td>
          </ng-container>
          <ng-container matColumnDef="suitable">
            <th mat-header-cell *matHeaderCellDef>Apto</th>
            <td mat-cell *matCellDef="let c">{{ c.isSuitable === true ? 'Sí' : c.isSuitable === false ? 'No' : '—' }}</td>
          </ng-container>
          <ng-container matColumnDef="hired">
            <th mat-header-cell *matHeaderCellDef>Contratado</th>
            <td mat-cell *matCellDef="let c">{{ c.isHired ? 'Sí' : 'No' }}</td>
          </ng-container>
          <ng-container matColumnDef="days">
            <th mat-header-cell *matHeaderCellDef class="num">Días de proceso</th>
            <td mat-cell *matCellDef="let c" class="num">{{ c.daysInProcess }}</td>
          </ng-container>
          <ng-container matColumnDef="salary">
            <th mat-header-cell *matHeaderCellDef class="num">Sueldo ofrecido</th>
            <td mat-cell *matCellDef="let c" class="num">{{ (c.offeredSalary | money) || '—' }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let c; columns: columns" class="clickable" tabindex="0" (click)="go(c.id)" (keydown.enter)="go(c.id)"></tr>
          <tr class="mat-mdc-row empty-row" *matNoDataRow>
            <td [attr.colspan]="columns.length">Sin candidatos con estos filtros.</td>
          </tr>
        </table>
      </div>
    </div>
  `,
})
export class CandidatesPage {
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly workspace = inject(ActivatedRoute).snapshot.data['workspace'] === true;
  readonly candidates = inject(CandidatesRepository).list();
  readonly stages = STAGES;

  readonly search = signal('');
  readonly opening = signal<string | null>(null);
  readonly stage = signal<CandidateStage | null>(null);
  readonly recruiter = signal<string | null>(null);

  readonly columns = this.workspace
    ? ['name', 'phone', 'opening', 'source', 'stage', 'suitable', 'hired', 'days', 'salary']
    : ['name', 'phone', 'opening', 'recruiter', 'source', 'stage', 'suitable', 'hired', 'days', 'salary'];
  readonly openingOptions = computed(() => [...new Set((this.candidates.value() ?? []).map((c) => c.openingName))].sort());
  readonly recruiterOptions = computed(() =>
    [...new Set((this.candidates.value() ?? []).map((c) => c.recruiterName).filter((n): n is string => !!n))].sort(),
  );
  readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
    return (this.candidates.value() ?? []).filter(
      (c) =>
        (!q ||
          c.fullName.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').includes(q) ||
          (c.mobilePhone ?? '').includes(q)) &&
        (!this.opening() || c.openingName === this.opening()) &&
        (!this.stage() || c.stage === this.stage()) &&
        (!this.recruiter() || c.recruiterName === this.recruiter()),
    );
  });

  stageOf(s: CandidateStage) {
    return STAGE[s];
  }

  sourceOf(c: CandidateListItemDto): string {
    return c.source ? SOURCE[c.source] : '—';
  }

  go(id: string): void {
    void this.router.navigate([this.workspace ? '/mis-candidatos' : '/candidatos', id]);
  }

  create(): void {
    openSidePanel<CandidatePanel, CandidatePanelData, CandidateDetailDto>(this.dialog, CandidatePanel, { workspace: this.workspace })
      .afterClosed()
      .subscribe((created) => {
        if (created) this.go(created.id);
      });
  }
}
