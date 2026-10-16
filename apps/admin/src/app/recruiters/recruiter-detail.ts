import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { API_CLIENT, confirm, DateKeyPipe, openSidePanel, Session } from '@platform/ui';
import type { RecruiterAssignmentDto, RecruiterDetailDto } from '../data/api-types';
import { OPENING_STATUS } from '../data/recruiting-labels';
import { OpeningsRepository, RecruitersRepository } from '../data/recruiting-repositories';
import { UsersRepository } from '../data/repositories';
import { CoverMode } from '../shell/cover-mode';
import { AssignOpeningPanel, type AssignOpeningData } from './assign-opening-panel';
import { conversion } from './recruiters-page';
import { NewLeadDialog, type NewLeadData } from './new-lead-dialog';

/**
 * «Detalle del reclutador» (SPEC §4.4, ARCHITECTURE §9.7 and §9.10): KPIs, Vacantes asignadas with «Hacer líder» / «Quitar»,
 * «Asignar vacante» and «Ver como reclutador» (cover mode: her workspace, every write audited as acting_for).
 */
@Component({
  selector: 'app-recruiter-detail',
  imports: [RouterLink, MatButton, MatIcon, MatProgressBar, MatSlideToggle, MatTableModule, DateKeyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton routerLink="/reclutadores" class="back"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>Reclutadores</a>
      @if (detail.loading() && !detail.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (detail.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (detail.value(); as d) {
        <header class="page-header">
          <h1>
            {{ d.recruiter.displayName }}
            <span class="subtitle">{{ d.recruiter.email }}@if (d.recruiter.phone) { · {{ d.recruiter.phone }} }</span>
          </h1>
          @if (canManageUsers) {
            <mat-slide-toggle [checked]="d.recruiter.isActive" (change)="setActive(d, $event.checked)">Activo</mat-slide-toggle>
          } @else {
            <span class="pill" [class.positive]="d.recruiter.isActive">{{ d.recruiter.isActive ? 'Activo' : 'Inactivo' }}</span>
          }
          <button matButton="outlined" type="button" (click)="assign(d)" [disabled]="!d.recruiter.isActive">
            <mat-icon fontSet="material-symbols-outlined">assignment_add</mat-icon>Asignar vacante
          </button>
          <button matButton="filled" type="button" (click)="cover(d)" [disabled]="!d.recruiter.isActive">
            <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>Ver como reclutador
          </button>
        </header>
        <div class="kpis">
          <div class="kpi"><div class="label">Vacantes asignadas</div><div class="value">{{ d.recruiter.openingsAssigned }}</div></div>
          <div class="kpi"><div class="label">Candidatos presentados</div><div class="value">{{ d.recruiter.candidatesPresented }}</div></div>
          <div class="kpi"><div class="label">Contratados</div><div class="value">{{ d.recruiter.candidatesHired }}</div></div>
          <div class="kpi"><div class="label">% conversión</div><div class="value">{{ conversion(d.recruiter) }}</div></div>
        </div>
        <h2 class="section">Vacantes asignadas</h2>
        <div class="table-card">
          <table mat-table [dataSource]="d.assignments">
            <ng-container matColumnDef="opening">
              <th mat-header-cell *matHeaderCellDef>Vacante</th>
              <td mat-cell *matCellDef="let a"><a [routerLink]="['/vacantes', a.openingId]">{{ a.displayName }}</a></td>
            </ng-container>
            <ng-container matColumnDef="role">
              <th mat-header-cell *matHeaderCellDef>Rol</th>
              <td mat-cell *matCellDef="let a"><span class="pill" [class.positive]="a.isLead">{{ a.isLead ? 'Líder' : 'Apoyo' }}</span></td>
            </ng-container>
            <ng-container matColumnDef="assigned">
              <th mat-header-cell *matHeaderCellDef>Fecha de asignación</th>
              <td mat-cell *matCellDef="let a">{{ a.assignedAt | dateKey }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Estatus</th>
              <td mat-cell *matCellDef="let a"><span class="pill" [class]="statusOf(a).tone">{{ statusOf(a).label }}</span></td>
            </ng-container>
            <ng-container matColumnDef="days">
              <th mat-header-cell *matHeaderCellDef class="num">Días abierta</th>
              <td mat-cell *matCellDef="let a" class="num">{{ a.daysOpen }}</td>
            </ng-container>
            <ng-container matColumnDef="candidates">
              <th mat-header-cell *matHeaderCellDef class="num">Candidatos</th>
              <td mat-cell *matCellDef="let a" class="num">{{ a.candidates }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let a" class="actions">
                @if (!a.isLead) {
                  <button matButton type="button" (click)="makeLead(d, a)">Hacer líder</button>
                }
                <button matButton type="button" class="destructive" (click)="remove(d, a)">Quitar</button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let a; columns: columns"></tr>
            <tr class="mat-mdc-row empty-row" *matNoDataRow>
              <td [attr.colspan]="columns.length">Sin vacantes asignadas. Usa «Asignar vacante».</td>
            </tr>
          </table>
        </div>
      }
    </div>
  `,
  styles: `
    .section {
      margin: 8px 0 12px;
      font: var(--mat-sys-title-medium);
    }
    .actions {
      text-align: right;
      white-space: nowrap;
    }
  `,
})
export class RecruiterDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly session = inject(Session);
  private readonly openings = inject(OpeningsRepository);
  private readonly coverMode = inject(CoverMode);
  private readonly api = inject(API_CLIENT);
  /** Route param, read from the snapshot: the live query starts in the field initializer, before inputs are bound. */
  private readonly recruiterId = this.route.snapshot.paramMap.get('id')!;
  readonly detail = inject(RecruitersRepository).detail(() => this.recruiterId);
  readonly conversion = conversion;
  readonly columns = ['opening', 'role', 'assigned', 'status', 'days', 'candidates', 'actions'];

  /** Activo is Usuarios' field: editable here only with that grant. */
  readonly canManageUsers = this.session.hasGrant('users');
  private readonly users = this.canManageUsers ? inject(UsersRepository).list() : null;
  private readonly user = computed(() => this.users?.value()?.find((u) => u.id === this.recruiterId) ?? null);
  private readonly usersRepo = inject(UsersRepository);

  statusOf(a: RecruiterAssignmentDto) {
    return OPENING_STATUS[a.status];
  }

  assign(d: RecruiterDetailDto): void {
    openSidePanel<AssignOpeningPanel, AssignOpeningData>(this.dialog, AssignOpeningPanel, { recruiter: d });
  }

  /** «Ver como reclutador»: her workspace with every write stamped acting_for (SPEC §4.4, PLAN D61). */
  cover(d: RecruiterDetailDto): void {
    this.coverMode.enter(d.recruiter.id, d.recruiter.displayName);
    this.api.cache.refreshAll();
    void this.router.navigateByUrl('/mis-vacantes');
  }

  async makeLead(d: RecruiterDetailDto, a: RecruiterAssignmentDto): Promise<void> {
    await this.attempt(() => this.openings.makeLead(a.openingId, d.recruiter.id), `${d.recruiter.displayName} es líder de ${a.displayName}`);
  }

  /** «Quitar»: a líder needs a successor among the other assigned recruiters, or «Sin líder» when none is left (PLAN D71). */
  async remove(d: RecruiterDetailDto, a: RecruiterAssignmentDto): Promise<void> {
    let newLeadId: string | null = null;
    if (a.isLead) {
      const opening = await this.openings.get(a.openingId);
      const others = opening.recruiters.filter((r) => r.userId !== d.recruiter.id);
      if (others.length > 0) {
        newLeadId = await new Promise<string | null>((resolve) =>
          this.dialog
            .open<NewLeadDialog, NewLeadData, string | null>(NewLeadDialog, {
              data: { opening: a.displayName, leaving: d.recruiter.displayName, candidates: others },
              width: 'min(440px, 100vw)',
            })
            .afterClosed()
            .subscribe((r) => resolve(r ?? null)),
        );
        if (!newLeadId) return;
      } else {
        const ok = await confirm(this.dialog, {
          title: '¿Quitar de la vacante?',
          body: `${a.displayName} se quedará sin líder; no hay otros reclutadores asignados.`,
          confirm: 'Quitar',
          destructive: true,
        });
        if (!ok) return;
      }
    } else {
      const ok = await confirm(this.dialog, {
        title: '¿Quitar de la vacante?',
        body: `${d.recruiter.displayName} dejará de ver ${a.displayName} en Mis vacantes.`,
        confirm: 'Quitar',
        destructive: true,
      });
      if (!ok) return;
    }
    await this.attempt(() => this.openings.remove(a.openingId, d.recruiter.id, newLeadId), 'Reclutador quitado de la vacante');
  }

  async setActive(d: RecruiterDetailDto, active: boolean): Promise<void> {
    const u = this.user();
    if (!u) return;
    await this.attempt(
      () =>
        this.usersRepo.update(u.id, {
          email: u.email,
          displayName: u.displayName,
          role: u.role,
          clientId: u.clientId ?? null,
          isActive: active,
          mfaRequired: u.mfaRequired,
          grants: null,
        }),
      `${d.recruiter.displayName}: ${active ? 'activo' : 'inactivo'}`,
    );
  }

  private async attempt(work: () => Promise<unknown>, done: string): Promise<void> {
    try {
      await work();
      this.snack.open(done, undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    }
  }
}
