import { inject, Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import type { MoveStageResultDto } from '../data/api-types';
import { STAGE, type CandidateStage } from '../data/recruiting-labels';
import { CandidatesRepository } from '../data/recruiting-repositories';
import { prompt } from '../openings/prompt-dialog';

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/**
 * A stage move from the Kanban or the candidate detail (SPEC §4.3, PLAN D72): asks the motivo for No apto / Descartado
 * and the fecha de ingreso for Contratado, then shows "HEB – Auxiliar de limpieza: 2 de 3 cubiertas" for a hire.
 */
@Injectable({ providedIn: 'root' })
export class StageMove {
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly candidates = inject(CandidatesRepository);

  /** Null when cancelled or refused (the API's message is shown). */
  async move(candidate: { id: string; fullName: string }, to: CandidateStage): Promise<MoveStageResultDto | null> {
    let reason: string | null = null;
    let hiredAt: string | null = null;
    if (to === 'not_suitable' || to === 'discarded') {
      reason = await prompt(this.dialog, {
        title: `${STAGE[to].label}: ${candidate.fullName}`,
        label: 'Motivo',
        confirm: `Marcar como ${STAGE[to].label.toLowerCase()}`,
        kind: 'text',
      });
      if (reason === null) return null;
    }
    if (to === 'hired') {
      hiredAt = await prompt(this.dialog, {
        title: `Contratar a ${candidate.fullName}`,
        body: 'La vacante suma un contratado; al llegar a las solicitadas queda Cubierta.',
        label: 'Fecha de ingreso',
        confirm: 'Marcar como contratado',
        kind: 'date',
        initial: today(),
      });
      if (hiredAt === null) return null;
    }
    try {
      const result = await this.candidates.move(candidate.id, to, reason, hiredAt);
      this.snack.open(
        to === 'hired'
          ? `${result.openingName}: ${result.hired} de ${result.headcountRequested} cubiertas`
          : `${candidate.fullName} → ${STAGE[to].label}`,
        undefined,
        { duration: 5000 },
      );
      return result;
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo mover al candidato.', undefined, { duration: 6000 });
      return null;
    }
  }
}
