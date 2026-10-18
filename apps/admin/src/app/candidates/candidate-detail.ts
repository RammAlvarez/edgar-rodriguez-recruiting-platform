import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { DateKeyPipe, MoneyPipe, openSidePanel, TimestampPipe } from '@platform/ui';
import { DocumentChecklist, type ChecklistDocument, type ChecklistItem } from '@platform/ui/document-checklist';
import type { CandidateDetailDto, InterviewDto, NoteDto, TimelineEntryDto } from '../data/api-types';
import { OUTCOME, SOURCE, STAGE, STAGES, type CandidateStage } from '../data/recruiting-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { CandidatesRepository, PeopleRepository } from '../data/recruiting-repositories';
import { prompt } from '../openings/prompt-dialog';
import { EmployeePanel, type EmployeePanelData } from '../staff/employee-panel';
import { InterviewPanel, type InterviewPanelData } from './interview-panel';
import { NoteEditor, type NoteDraft } from './note-editor';
import { PersonPanel, type PersonPanelData } from './person-panel';
import { StageMove } from './stage-move';

/**
 * «Perfil del candidato» (SPEC §4.2, ARCHITECTURE §9 2b): header with name, vacante, etapa, reclutador; left column
 * Datos personales and Notas (rich text); right column Documentos · Entrevistas · Historial. Same screen in the recruiter
 * workspace (route data `workspace`). Opening it is logged as a profile read.
 */
@Component({
  selector: 'app-candidate-detail',
  imports: [
    RouterLink,
    MatButton,
    MatIcon,
    MatMenu,
    MatMenuItem,
    MatMenuTrigger,
    MatProgressBar,
    MatTabGroup,
    MatTab,
    DocumentChecklist,
    NoteEditor,
    MoneyPipe,
    DateKeyPipe,
    TimestampPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton [routerLink]="base" class="back">
        <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>{{ workspace ? 'Mis candidatos' : 'Candidatos' }}
      </a>
      @if (candidate.loading() && !candidate.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (candidate.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (candidate.value(); as c) {
        <header class="page-header">
          <h1>
            {{ c.person.fullName }}
            <span class="subtitle">
              <a [routerLink]="[openingBase, c.openingId]">{{ c.openingName }}</a> · Reclutador: {{ c.recruiterName ?? 'Sin reclutador' }}
            </span>
          </h1>
          <button matButton="outlined" type="button" [matMenuTriggerFor]="stages" [disabled]="!canMove(c)">
            <span class="pill" [class]="stageOf(c.stage).tone">{{ stageOf(c.stage).label }}</span>
            <mat-icon fontSet="material-symbols-outlined" iconPositionEnd>expand_more</mat-icon>
          </button>
          <mat-menu #stages="matMenu" xPosition="before">
            @for (s of allStages; track s) {
              @if (s !== c.stage) {
                <button mat-menu-item type="button" (click)="move(c, s)">Mover a {{ stageOf(s).label }}</button>
              }
            }
          </mat-menu>
          @if (c.employeeId) {
            <a matButton="outlined" [routerLink]="['/personal', c.employeeId]">
              <mat-icon fontSet="material-symbols-outlined">badge</mat-icon>Ver empleado
            </a>
          } @else if (c.canConvertToEmployee) {
            <button matButton="filled" type="button" (click)="convert(c)">
              <mat-icon fontSet="material-symbols-outlined">badge</mat-icon>Convertir en empleado
            </button>
          }
        </header>
        @if (c.discardReason) {
          <p class="muted reason">Motivo: {{ c.discardReason }}</p>
        }
        <div class="columns">
          <div class="left">
            <section class="card">
              <div class="card-header">
                <h2>Datos personales</h2>
                <button matButton type="button" (click)="editPerson(c)">
                  <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>Editar
                </button>
              </div>
              <dl>
                <dt>Teléfono</dt><dd>{{ c.person.mobilePhone ?? '—' }}</dd>
                <dt>Correo</dt><dd>{{ c.person.email ?? '—' }}</dd>
                <dt>Domicilio</dt><dd>{{ address(c) || '—' }}</dd>
                <dt>CURP</dt><dd class="mono">{{ c.person.curp ?? '—' }}</dd>
                <dt>RFC</dt><dd class="mono">{{ c.person.rfc ?? '—' }}</dd>
                <dt>Fecha de nacimiento</dt><dd>{{ (c.person.birthDate | dateKey) || '—' }}</dd>
                <dt>Fuente</dt><dd>{{ c.source ? source[c.source] : '—' }}</dd>
                <dt>Fecha de captura</dt><dd>{{ c.capturedAt | dateKey }}</dd>
                <dt>Días de proceso</dt><dd>{{ c.daysInProcess }}</dd>
                <dt>Sueldo ofrecido</dt><dd>{{ (c.offeredSalary | money: true) || '—' }}</dd>
                @if (c.hiredAt) {
                  <dt>Fecha de ingreso</dt><dd>{{ c.hiredAt | dateKey }}</dd>
                }
              </dl>
              @if (c.person.email) {
                <button matButton type="button" class="invite" (click)="invite(c)" [disabled]="inviting()">
                  <mat-icon fontSet="material-symbols-outlined">forward_to_inbox</mat-icon>Reenviar invitación a Mi postulación
                </button>
              }
            </section>
            <section class="card">
              <div class="card-header"><h2>Notas</h2></div>
              <app-note-editor #editor [busy]="savingNote()" (saved)="saveNote(c, $event)" (cancelled)="editingNote.set(null)" />
              <div class="notes">
                @for (n of notes.value() ?? []; track n.id) {
                  <article class="note">
                    <div class="note-body" [innerHTML]="n.contentHtml"></div>
                    <div class="muted note-footer">
                      {{ n.author }} · {{ n.createdAt | timestamp }}
                      @if (n.editedAt) { · editada }
                      @if (n.canEdit) {
                        <button matButton type="button" (click)="editNote(n)">Editar</button>
                      }
                    </div>
                  </article>
                } @empty {
                  <p class="muted">Sin notas.</p>
                }
              </div>
            </section>
          </div>
          <div class="right">
            <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
              <mat-tab label="Documentos">
                @if (checklist.value(); as list) {
                  <ui-document-checklist [checklist]="list" [canReview]="true" [uploading]="uploading()"
                                         (upload)="upload(c, $event.item, $event.file)" (view)="view($event)"
                                         (validate)="validate($event)" (reject)="reject($event)" />
                } @else if (checklist.error(); as e) {
                  <p class="form-alert">{{ e.message }}</p>
                }
              </mat-tab>
              <mat-tab label="Entrevistas">
                <div class="tab-actions">
                  <button matButton="filled" type="button" (click)="interview(c, null)">
                    <mat-icon fontSet="material-symbols-outlined">event</mat-icon>Programar entrevista
                  </button>
                </div>
                <div class="list-card">
                  @for (i of interviews.value() ?? []; track i.id) {
                    <div class="list-row clickable" tabindex="0" (click)="interview(c, i)" (keydown.enter)="interview(c, i)">
                      <mat-icon fontSet="material-symbols-outlined" class="muted">event</mat-icon>
                      <div class="grow">
                        <div class="title">{{ i.scheduledAt | timestamp }}</div>
                        <div class="muted">{{ i.interviewerName ?? 'Sin entrevistador' }} · {{ i.location ?? 'Sin lugar' }}</div>
                      </div>
                      <span class="pill" [class.positive]="i.outcome === 'attended'" [class.negative]="i.outcome === 'no_show'">
                        {{ outcome[i.outcome] }}
                      </span>
                    </div>
                  } @empty {
                    <p class="list-empty">Sin entrevistas.</p>
                  }
                </div>
              </mat-tab>
              <mat-tab label="Historial">
                <div class="list-card history">
                  @for (h of history.value() ?? []; track $index) {
                    <div class="list-row">
                      <mat-icon fontSet="material-symbols-outlined" class="muted">{{ h.kind === 'note' ? 'sticky_note_2' : h.kind === 'interview' ? 'event' : 'sync_alt' }}</mat-icon>
                      <div class="grow">
                        <div class="title">{{ describe(h) }}</div>
                        <div class="muted">{{ h.author ?? 'Sistema' }} · {{ h.at | timestamp }}</div>
                      </div>
                    </div>
                  }
                </div>
              </mat-tab>
            </mat-tab-group>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .reason {
      margin: -16px 0 16px;
    }
    .columns {
      display: grid;
      grid-template-columns: minmax(300px, 5fr) 7fr;
      gap: 24px;
      align-items: start;
    }
    @media (max-width: 959.98px) {
      .columns {
        grid-template-columns: 1fr;
      }
    }
    .left {
      display: flex;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
    }
    .right {
      min-width: 0;
    }
    .card {
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-medium);
      }
    }
    .card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;
    }
    .invite {
      margin-top: 8px;
    }
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 6px 16px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .notes {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-top: 16px;
    }
    .note {
      padding-top: 12px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    .note-body :first-child {
      margin-top: 0;
    }
    .note-footer {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 4px;
    }
    .history {
      margin-top: 16px;
    }
  `,
})
export class CandidateDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly employees = inject(EmployeesRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly repo = inject(CandidatesRepository);
  private readonly people = inject(PeopleRepository);
  private readonly stageMove = inject(StageMove);
  /** Route param, read from the snapshot: the live queries start in field initializers, before inputs are bound. */
  private readonly candidateId = this.route.snapshot.paramMap.get('id')!;
  readonly workspace = this.route.snapshot.data['workspace'] === true;
  readonly base = this.workspace ? '/mis-candidatos' : '/candidatos';
  readonly openingBase = this.workspace ? '/mis-vacantes' : '/vacantes';

  readonly candidate = this.repo.detail(() => this.candidateId);
  readonly notes = this.repo.notes(() => this.candidateId);
  readonly interviews = this.repo.interviews(() => this.candidateId);
  readonly history = this.repo.history(() => this.candidateId);
  private readonly personId = computed(() => this.candidate.value()?.person.id ?? null);
  readonly checklist = this.people.checklist(
    () => this.personId(),
    () => this.candidate.value()?.documentTemplateId ?? null,
  );

  private readonly editor = viewChild<NoteEditor>('editor');
  readonly allStages = STAGES;
  readonly source = SOURCE;
  readonly outcome = OUTCOME;
  readonly savingNote = signal(false);
  readonly editingNote = signal<string | null>(null);
  readonly uploading = signal<{ documentTypeId: string; progress: number } | null>(null);

  constructor() {
    // The checklist needs the person id, which arrives with the candidate: load it once the id is known.
    effect(() => {
      if (this.personId()) untracked(() => this.checklist.reload());
    });
  }

  stageOf(s: CandidateStage) {
    return STAGE[s];
  }

  canMove(c: CandidateDetailDto): boolean {
    return c.openingStatus !== 'paused' && c.openingStatus !== 'cancelled';
  }

  address(c: CandidateDetailDto): string {
    const p = c.person;
    return [p.street && `${p.street} ${p.extNumber ?? ''}`.trim(), p.neighborhood, p.postalCode && `CP ${p.postalCode}`, p.municipality, p.state]
      .filter(Boolean)
      .join(', ');
  }

  describe(h: TimelineEntryDto): string {
    if (h.kind !== 'stage') return h.text ?? '';
    const to = h.toStage ? STAGE[h.toStage].label : '';
    const text = h.fromStage ? `${STAGE[h.fromStage].label} → ${to}` : `Capturado como ${to}`;
    return h.text ? `${text} · ${h.text}` : text;
  }

  async move(c: CandidateDetailDto, to: CandidateStage): Promise<void> {
    await this.stageMove.move({ id: c.id, fullName: c.person.fullName }, to);
  }

  editPerson(c: CandidateDetailDto): void {
    openSidePanel<PersonPanel, PersonPanelData>(this.dialog, PersonPanel, { person: c.person });
  }

  interview(c: CandidateDetailDto, i: InterviewDto | null): void {
    openSidePanel<InterviewPanel, InterviewPanelData>(this.dialog, InterviewPanel, { candidateId: c.id, interview: i });
  }

  editNote(n: NoteDto): void {
    this.editingNote.set(n.id);
    this.editor()?.load(n.contentJson ?? null, n.contentHtml);
  }

  async saveNote(c: CandidateDetailDto, draft: NoteDraft): Promise<void> {
    this.savingNote.set(true);
    try {
      await this.repo.saveNote(c.id, this.editingNote(), draft.html, draft.json);
      this.editor()?.reset();
      this.editingNote.set(null);
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo guardar la nota.', undefined, { duration: 6000 });
    } finally {
      this.savingNote.set(false);
    }
  }

  async upload(c: CandidateDetailDto, item: ChecklistItem, file: File): Promise<void> {
    this.uploading.set({ documentTypeId: item.documentTypeId, progress: 0 });
    try {
      await this.people.upload(c.person.id, item, file, (progress) => this.uploading.set({ documentTypeId: item.documentTypeId, progress }));
      this.snack.open(`${item.nameEs} entregado`, undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo subir el archivo.', undefined, { duration: 6000 });
    } finally {
      this.uploading.set(null);
    }
  }

  async view(d: ChecklistDocument): Promise<void> {
    // Open the tab first (popup blockers allow it only inside the click), then point it at the presigned URL.
    const tab = window.open('', '_blank');
    try {
      const { url } = await this.people.url(d.id, false);
      if (tab) tab.location.href = url;
      else window.open(url, '_blank', 'noopener');
    } catch (e) {
      tab?.close();
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo abrir el documento.', undefined, { duration: 6000 });
    }
  }

  async validate(d: ChecklistDocument): Promise<void> {
    await this.attempt(() => this.people.validate(d.id), 'Documento validado');
  }

  async reject(d: ChecklistDocument): Promise<void> {
    const reason = await prompt(this.dialog, { title: 'Rechazar documento', label: 'Motivo', confirm: 'Rechazar', kind: 'text', destructive: true, maxLength: 300 });
    if (reason) await this.attempt(() => this.people.reject(d.id, reason), 'Documento rechazado');
  }

  /** «Convertir en empleado» (SPEC §4.2, S7): the alta pre-filled with the hire date and the vacante's card bank. */
  async convert(c: CandidateDetailDto): Promise<void> {
    try {
      const conversion = await this.employees.conversion(c.id);
      openSidePanel<EmployeePanel, EmployeePanelData>(this.dialog, EmployeePanel, { mode: 'convert', conversion });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    }
  }

  readonly inviting = signal(false);

  /** The capture invitation again (the first one goes out with «Nuevo candidato», PLAN D77). */
  async invite(c: CandidateDetailDto): Promise<void> {
    this.inviting.set(true);
    try {
      const r = await this.repo.invite(c.id);
      this.snack.open(r.sent ? `Invitación enviada a ${r.email}` : 'No se pudo enviar el correo; inténtalo más tarde.', undefined,
        { duration: r.sent ? 4000 : 6000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    } finally {
      this.inviting.set(false);
    }
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
