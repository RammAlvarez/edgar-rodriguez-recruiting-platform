import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { DateKeyPipe, MoneyPipe, openSidePanel, Pager, SortValue, TimestampPipe, UiPager } from '@platform/ui';
import type { CandidateDetailDto, CandidateListItemDto, OpeningDetailDto, OpeningEventDto } from '../data/api-types';
import { CandidatePanel, type CandidatePanelData } from '../candidates/candidate-panel';
import { OPENING_STATUS, PRIORITY, SOURCE, STAGE, type OpeningEventKind, type OpeningStatus } from '../data/recruiting-labels';
import { CandidatesRepository, OpeningsRepository } from '../data/recruiting-repositories';
import { CatalogsRepository } from '../data/repositories';
import { AssignRecruiterPanel, type AssignRecruiterData } from './assign-recruiter-panel';
import { Kanban } from './kanban';
import { OpeningPanel, type OpeningPanelData } from './opening-panel';
import { prompt } from './prompt-dialog';

/**
 * «Detalle de vacante» (SPEC §4.1, ARCHITECTURE §9 2a): "Cliente – Puesto", status, counters, líder; tabs Pipeline (Kanban),
 * Candidatos, Documentos requeridos, Comentarios del cliente, Actividad. In «Mis vacantes» (route data `workspace`) the
 * Administrador actions are hidden: a Reclutador only comments (SPEC §4.8).
 */
@Component({
  selector: 'app-opening-detail',
  imports: [MatSortModule, UiPager, 
    FormsModule,
    RouterLink,
    MatButton,
    MatIcon,
    MatProgressBar,
    MatTabGroup,
    MatTab,
    MatTableModule,
    MatMenu,
    MatMenuItem,
    MatMenuTrigger,
    MatFormField,
    MatLabel,
    MatInput,
    MatCheckbox,
    Kanban,
    MoneyPipe,
    DateKeyPipe,
    TimestampPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton [routerLink]="base" class="back">
        <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>{{ workspace ? 'Mis vacantes' : 'Vacantes' }}
      </a>
      @if (opening.loading() && !opening.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (opening.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (opening.value(); as o) {
        <header class="page-header">
          <h1>
            {{ o.displayName }}
            <span class="subtitle">
              <span class="pill" [class]="status(o.status).tone">{{ status(o.status).label }}</span>
              Prioridad {{ priority[o.priority] }} · {{ o.zoneName ?? 'Sin zona' }} · Líder: {{ o.leadRecruiterName ?? 'Sin líder' }}
              @if (support(o); as s) { · Apoyo: {{ s }} }
            </span>
          </h1>
          @if (!workspace) {
            <button matButton="outlined" type="button" (click)="assign(o)">
              <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Asignar reclutador
            </button>
            <button matButton="outlined" type="button" (click)="edit(o)">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>Editar
            </button>
            <button matButton type="button" [matMenuTriggerFor]="more" aria-label="Más acciones">
              <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>Más
            </button>
            <mat-menu #more="matMenu" xPosition="before">
              <button mat-menu-item type="button" (click)="duplicate(o)">
                <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon><span>Duplicar vacante</span>
              </button>
              @if (o.status === 'open' || o.status === 'in_progress') {
                <button mat-menu-item type="button" (click)="setStatus(o, 'paused')">
                  <mat-icon fontSet="material-symbols-outlined">pause_circle</mat-icon><span>Pausar</span>
                </button>
              }
              @if (o.status === 'open' || o.status === 'in_progress' || o.status === 'paused') {
                <button mat-menu-item type="button" (click)="setStatus(o, 'cancelled')">
                  <mat-icon fontSet="material-symbols-outlined">cancel</mat-icon><span>Cancelar vacante</span>
                </button>
              }
              @if (o.status === 'paused' || o.status === 'cancelled') {
                <button mat-menu-item type="button" (click)="setStatus(o, 'open')">
                  <mat-icon fontSet="material-symbols-outlined">play_circle</mat-icon><span>Reabrir</span>
                </button>
              }
            </mat-menu>
          }
        </header>
        <div class="kpis">
          <div class="kpi"><div class="label">Solicitadas</div><div class="value">{{ o.headcountRequested }}</div></div>
          <div class="kpi"><div class="label">Contratados</div><div class="value">{{ o.hired }}</div></div>
          <div class="kpi"><div class="label">Candidatos</div><div class="value">{{ total(o) }}</div></div>
          <div class="kpi"><div class="label">Días abierta</div><div class="value">{{ o.daysOpen }}</div></div>
          <div class="kpi"><div class="label">Sueldo mensual</div><div class="value">{{ o.monthlySalary | money: true }}</div></div>
        </div>
        <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
          <mat-tab label="Pipeline">
            <div class="tab-actions">
              @if (o.status === 'open' || o.status === 'in_progress') {
                <button matButton="filled" type="button" (click)="newCandidate(o)">
                  <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Nuevo candidato
                </button>
              }
            </div>
            <app-kanban [candidates]="candidates.value() ?? []" [canMove]="o.status !== 'paused' && o.status !== 'cancelled'"
                        [candidateBase]="candidateBase" />
          </mat-tab>
          <mat-tab label="Candidatos">
            <div class="table-card tab-table">
              <table mat-table matSort [matSortActive]="pager.sort().active" [matSortDirection]="pager.sort().direction" (matSortChange)="pager.sortChange($event)" [dataSource]="pager.slice(candidates.value() ?? [], sortBy)">
                <ng-container matColumnDef="name">
                  <th mat-header-cell mat-sort-header="name" *matHeaderCellDef>Nombre</th>
                  <td mat-cell *matCellDef="let c"><strong>{{ c.fullName }}</strong></td>
                </ng-container>
                <ng-container matColumnDef="stage">
                  <th mat-header-cell mat-sort-header="stage" *matHeaderCellDef>Etapa</th>
                  <td mat-cell *matCellDef="let c"><span class="pill" [class]="stageOf(c).tone">{{ stageOf(c).label }}</span></td>
                </ng-container>
                <ng-container matColumnDef="source">
                  <th mat-header-cell mat-sort-header="source" *matHeaderCellDef>Fuente</th>
                  <td mat-cell *matCellDef="let c">{{ sourceOf(c) }}</td>
                </ng-container>
                <ng-container matColumnDef="recruiter">
                  <th mat-header-cell mat-sort-header="recruiter" *matHeaderCellDef>Reclutador</th>
                  <td mat-cell *matCellDef="let c">{{ c.recruiterName ?? 'Sin reclutador' }}</td>
                </ng-container>
                <ng-container matColumnDef="days">
                  <th mat-header-cell mat-sort-header="days" *matHeaderCellDef class="num">Días de proceso</th>
                  <td mat-cell *matCellDef="let c" class="num">{{ c.daysInProcess }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="candidateColumns"></tr>
                <tr mat-row *matRowDef="let c; columns: candidateColumns" class="clickable" tabindex="0"
                    (click)="openCandidate(c.id)" (keydown.enter)="openCandidate(c.id)"></tr>
                <tr class="mat-mdc-row empty-row" *matNoDataRow>
                  <td [attr.colspan]="candidateColumns.length">Sin candidatos todavía.</td>
                </tr>
              </table>
              <ui-pager [pager]="pager" [total]="(candidates.value() ?? []).length" />
            </div>
          </mat-tab>
          <mat-tab label="Documentos requeridos">
            <div class="list-card tab-table">
              <p class="list-empty">Plantilla: <strong>{{ template(o)?.nameEs ?? '—' }}</strong></p>
              @for (i of templateItems(o); track i.code) {
                <div class="list-row">
                  <mat-icon fontSet="material-symbols-outlined" class="muted">description</mat-icon>
                  <div class="grow"><div class="title">{{ i.name }}</div></div>
                  <span class="pill" [class.positive]="i.required">{{ i.required ? 'Obligatorio' : 'Opcional' }}</span>
                </div>
              }
            </div>
          </mat-tab>
          <mat-tab label="Comentarios del cliente">
            <div class="list-card tab-table">
              @for (c of comments.value() ?? []; track c.id) {
                <div class="list-row">
                  <div class="grow">
                    <div class="title">{{ c.author }} <span class="muted">{{ c.createdAt | timestamp }}</span></div>
                    <div>{{ c.body }}</div>
                  </div>
                  @if (!c.visibleToClient) {
                    <span class="pill">Interno</span>
                  }
                </div>
              } @empty {
                <p class="list-empty">Sin comentarios. El cliente verá este hilo en su portal.</p>
              }
            </div>
            <form class="comment-form" (ngSubmit)="postComment(o)">
              <mat-form-field class="grow">
                <mat-label>Escribe un comentario</mat-label>
                <textarea matInput name="comment" [(ngModel)]="commentText" rows="2" maxlength="2000"></textarea>
              </mat-form-field>
              <mat-checkbox name="visible" [(ngModel)]="commentVisible">Visible para el cliente</mat-checkbox>
              <button matButton="filled" type="submit" [disabled]="!commentText.trim() || posting()">Publicar</button>
            </form>
          </mat-tab>
          <mat-tab label="Actividad">
            <div class="list-card tab-table">
              @for (e of events.value() ?? []; track e.id) {
                <div class="list-row">
                  <mat-icon fontSet="material-symbols-outlined" class="muted">{{ icon(e) }}</mat-icon>
                  <div class="grow">
                    <div class="title">{{ describe(e) }}</div>
                    <div class="muted">{{ e.author ?? 'Sistema' }} · {{ e.occurredAt | timestamp }}</div>
                  </div>
                </div>
              } @empty {
                <p class="list-empty">Sin actividad.</p>
              }
            </div>
          </mat-tab>
        </mat-tab-group>
        <section class="details">
          <h2>Datos de la vacante</h2>
          <dl>
            <dt>Fecha de solicitud</dt><dd>{{ o.requestedAt | dateKey }}</dd>
            <dt>Fecha cubierta</dt><dd>{{ (o.filledAt | dateKey) || '—' }}</dd>
            <dt>Horario</dt><dd>{{ o.schedule ?? '—' }}</dd>
            <dt>Prestaciones</dt><dd>{{ o.benefits.join(', ') || '—' }}</dd>
            <dt>Banco de pago</dt><dd>{{ banksOf(o) || '—' }}</dd>
            <dt>Nivel de estudios</dt><dd>{{ educationOf(o) ?? '—' }}</dd>
            <dt>Experiencia</dt><dd>{{ o.yearsExperience ?? 0 }} años · {{ o.requiredExperience ?? '—' }}</dd>
            @if (!workspace) {
              <dt>Sexo / Edad (interno)</dt><dd>{{ gender(o) }} · {{ o.ageRange ?? '—' }}</dd>
            }
            <dt>Observaciones</dt><dd>{{ o.notes ?? '—' }}</dd>
          </dl>
        </section>
      }
    </div>
  `,
  styles: `
    .subtitle .pill {
      margin-right: 8px;
    }
    .tab-table {
      margin-top: 16px;
    }
    .comment-form {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-top: 12px;
      .grow {
        flex: 1;
        min-width: 240px;
      }
    }
    .details {
      margin-top: 32px;
      h2 {
        font: var(--mat-sys-title-medium);
      }
      dl {
        display: grid;
        grid-template-columns: minmax(140px, max-content) 1fr;
        gap: 6px 16px;
        margin: 0;
      }
      dt {
        color: var(--mat-sys-on-surface-variant);
      }
      dd {
        margin: 0;
      }
    }
  `,
})
export class OpeningDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly repo = inject(OpeningsRepository);
  /** Route param, read from the snapshot: the live queries start in field initializers, before inputs are bound. */
  private readonly openingId = this.route.snapshot.paramMap.get('id')!;
  readonly workspace = this.route.snapshot.data['workspace'] === true;
  readonly base = this.workspace ? '/mis-vacantes' : '/vacantes';
  readonly candidateBase = this.workspace ? '/mis-candidatos' : '/candidatos';

  readonly opening = this.repo.detail(() => this.openingId);
  readonly sortBy: Record<string, (c: CandidateListItemDto) => SortValue> = {
    name: (c) => c.fullName,
    stage: (c) => this.stageOf(c).label,
    source: (c) => this.sourceOf(c),
    recruiter: (c) => c.recruiterName,
    days: (c) => c.daysInProcess,
  };
  readonly pager = new Pager();
  readonly candidates = inject(CandidatesRepository).list(() => this.openingId);
  readonly events = this.repo.events(() => this.openingId);
  readonly comments = this.repo.comments(() => this.openingId);
  private readonly catalogs = inject(CatalogsRepository).catalogs();

  readonly priority = PRIORITY;
  readonly candidateColumns = ['name', 'stage', 'source', 'recruiter', 'days'];
  readonly posting = signal(false);
  commentText = '';
  commentVisible = true;

  readonly genders = { unspecified: 'Indistinto', male: 'Masculino', female: 'Femenino' } as const;

  stageOf(c: CandidateListItemDto) {
    return STAGE[c.stage];
  }

  sourceOf(c: CandidateListItemDto): string {
    return c.source ? SOURCE[c.source] : '—';
  }

  status(s: OpeningStatus) {
    return OPENING_STATUS[s];
  }

  total(o: OpeningDetailDto): number {
    return o.stageCounts.reduce((sum, s) => sum + Number(s.count), 0);
  }

  support(o: OpeningDetailDto): string {
    return o.recruiters.filter((r) => !r.isLead).map((r) => r.displayName).join(', ');
  }

  gender(o: OpeningDetailDto): string {
    return this.genders[o.genderRequirement];
  }

  readonly template = (o: OpeningDetailDto) => {
    const all = this.catalogs.value()?.documentTemplates ?? [];
    return o.documentTemplateId ? all.find((t) => t.id === o.documentTemplateId) : all.find((t) => t.scope === 'candidate' && t.isDefault);
  };

  templateItems(o: OpeningDetailDto) {
    const types = new Map((this.catalogs.value()?.documentTypes ?? []).map((t) => [t.id, t]));
    return (this.template(o)?.items ?? []).map((i) => ({
      code: types.get(i.documentTypeId)?.code ?? i.documentTypeId,
      name: types.get(i.documentTypeId)?.nameEs ?? '—',
      required: i.isRequired,
    }));
  }

  banksOf(o: OpeningDetailDto): string {
    const banks = new Map((this.catalogs.value()?.banks ?? []).map((b) => [b.id, b.name]));
    return o.paymentBankIds.map((id) => banks.get(id) ?? '').filter(Boolean).join(', ');
  }

  educationOf(o: OpeningDetailDto): string | null {
    return (this.catalogs.value()?.educationLevels ?? []).find((e) => e.id === o.educationLevelId)?.nameEs ?? null;
  }

  icon(e: OpeningEventDto): string {
    const icons: Record<OpeningEventKind, string> = {
      created: 'add_circle',
      edited: 'edit',
      status_changed: 'sync_alt',
      recruiter_assigned: 'person_add',
      recruiter_removed: 'person_remove',
      lead_changed: 'star',
    };
    return icons[e.kind];
  }

  describe(e: OpeningEventDto): string {
    switch (e.kind) {
      case 'created':
        return 'Vacante creada';
      case 'edited':
        return 'Datos de la vacante actualizados';
      case 'status_changed':
        return `Estatus: ${e.fromStatus ? OPENING_STATUS[e.fromStatus].label : '—'} → ${e.toStatus ? OPENING_STATUS[e.toStatus].label : '—'}`
          + (e.reason ? ` · ${e.reason}` : '');
      case 'recruiter_assigned':
        return `Asignada a ${e.recruiterName ?? '—'} como apoyo`;
      case 'recruiter_removed':
        return `${e.recruiterName ?? '—'} ya no está asignada`;
      case 'lead_changed':
        return `Asignada a ${e.recruiterName ?? '—'} como líder`;
      default:
        return '';
    }
  }

  openCandidate(id: string): void {
    void this.router.navigate([this.candidateBase, id]);
  }

  edit(o: OpeningDetailDto): void {
    openSidePanel<OpeningPanel, OpeningPanelData>(this.dialog, OpeningPanel, { opening: o });
  }

  assign(o: OpeningDetailDto): void {
    openSidePanel<AssignRecruiterPanel, AssignRecruiterData>(this.dialog, AssignRecruiterPanel, { opening: o });
  }

  newCandidate(o: OpeningDetailDto): void {
    openSidePanel<CandidatePanel, CandidatePanelData, CandidateDetailDto>(this.dialog, CandidatePanel, {
      openingId: o.id,
      workspace: this.workspace,
    });
  }

  async duplicate(o: OpeningDetailDto): Promise<void> {
    try {
      const copy = await this.repo.duplicate(o.id);
      this.snack.open('Vacante duplicada', undefined, { duration: 4000 });
      void this.router.navigate([this.base, copy.id]);
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo duplicar.', undefined, { duration: 6000 });
    }
  }

  async setStatus(o: OpeningDetailDto, status: OpeningStatus): Promise<void> {
    let reason: string | null = null;
    if (status === 'paused' || status === 'cancelled') {
      reason = await prompt(this.dialog, {
        title: status === 'paused' ? '¿Pausar la vacante?' : '¿Cancelar la vacante?',
        body: status === 'paused'
          ? 'No se podrán mover candidatos hasta reabrirla.'
          : 'La vacante deja de recibir candidatos. Puedes reabrirla después.',
        label: 'Motivo',
        confirm: status === 'paused' ? 'Pausar' : 'Cancelar vacante',
        kind: 'text',
        destructive: status === 'cancelled',
      });
      if (reason === null) return;
    }
    try {
      const saved = await this.repo.changeStatus(o.id, status, reason);
      this.snack.open(`Vacante ${OPENING_STATUS[saved.status].label.toLowerCase()}`, undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo cambiar el estatus.', undefined, { duration: 6000 });
    }
  }

  async postComment(o: OpeningDetailDto): Promise<void> {
    const text = this.commentText.trim();
    if (!text) return;
    this.posting.set(true);
    try {
      await this.repo.postComment(o.id, text, this.commentVisible);
      this.commentText = '';
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo publicar.', undefined, { duration: 6000 });
    } finally {
      this.posting.set(false);
    }
  }
}
