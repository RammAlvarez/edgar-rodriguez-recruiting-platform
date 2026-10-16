import { CdkDrag, type CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { Router } from '@angular/router';
import type { CandidateListItemDto } from '../data/api-types';
import { SOURCE, STAGE, STAGES, type CandidateStage } from '../data/recruiting-labels';
import { StageMove } from '../candidates/stage-move';

/**
 * The vacante's Pipeline (SPEC §4.2): one column per etapa, cards dragged between them. A drop asks what the stage needs
 * (motivo, fecha de ingreso) and moves the card only when the API accepts; counters follow the live query.
 */
@Component({
  selector: 'app-kanban',
  imports: [CdkDropListGroup, CdkDropList, CdkDrag, MatButton, MatIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kanban" cdkDropListGroup>
      @for (stage of stages; track stage) {
        <div class="kanban-column" cdkDropList [id]="stage" [cdkDropListData]="stage" [cdkDropListDisabled]="!canMove()"
             (cdkDropListDropped)="drop($event)">
          <div class="column-title">
            <span>{{ label[stage].label }}</span><span class="muted">{{ byStage()[stage].length }}</span>
          </div>
          @for (c of byStage()[stage]; track c.id) {
            <div class="kanban-card" cdkDrag [cdkDragData]="c" [cdkDragDisabled]="!canMove() || busy() === c.id"
                 tabindex="0" (keydown.enter)="open(c)" (dblclick)="open(c)">
              <div class="name">{{ c.fullName }}</div>
              <div class="muted">{{ source[c.source!] ?? '—' }} · {{ c.daysInProcess }} d</div>
              <div class="card-actions">
                <button matButton type="button" (click)="open(c)">Ver perfil</button>
                @if (c.stage === 'hired') {
                  <!-- «Convertir en empleado» lives on the candidate's profile, which knows whether it was converted. -->
                  <button matButton type="button" (click)="open(c)">
                    <mat-icon fontSet="material-symbols-outlined">badge</mat-icon>Convertir
                  </button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .card-actions {
      display: flex;
      flex-wrap: wrap;
      margin: 4px -8px -6px;
    }
  `,
})
export class Kanban {
  readonly candidates = input.required<readonly CandidateListItemDto[]>();
  readonly canMove = input(true);
  /** Base route of the candidate detail (candidatos or mis-candidatos). */
  readonly candidateBase = input('/candidatos');
  readonly moved = output<void>();

  private readonly stageMove = inject(StageMove);
  private readonly router = inject(Router);
  readonly stages = STAGES;
  readonly label = STAGE;
  readonly source = SOURCE;
  readonly busy = signal<string | null>(null);

  readonly byStage = computed(() => {
    const groups = Object.fromEntries(STAGES.map((s) => [s, [] as CandidateListItemDto[]])) as Record<CandidateStage, CandidateListItemDto[]>;
    for (const c of this.candidates()) groups[c.stage].push(c);
    return groups;
  });

  async drop(e: CdkDragDrop<CandidateStage, CandidateStage, CandidateListItemDto>): Promise<void> {
    const to = e.container.data;
    const c = e.item.data;
    if (e.previousContainer === e.container || c.stage === to) return;
    this.busy.set(c.id);
    try {
      if (await this.stageMove.move(c, to)) this.moved.emit();
    } finally {
      this.busy.set(null);
    }
  }

  open(c: CandidateListItemDto): void {
    void this.router.navigate([this.candidateBase(), c.id]);
  }
}
