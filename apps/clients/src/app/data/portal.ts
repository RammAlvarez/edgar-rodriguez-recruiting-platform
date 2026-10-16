import { inject, Injectable } from '@angular/core';
import type { components } from '@platform/shared';
import { API_CLIENT } from '@platform/ui/api';
import { liveQuery } from '@platform/ui/live-query';

type S = components['schemas'];
export type ClientOpeningDto = S['ClientOpeningDto'];
export type ClientOpeningDetailDto = S['ClientOpeningDetailDto'];
export type ClientCandidateDto = S['ClientCandidateDto'];
export type ClientCommentDto = S['ClientCommentDto'];
// NonNullable: the OpenAPI document adds null to an enum's schema when some property using it is nullable.
export type OpeningStatus = NonNullable<S['OpeningStatus']>;
export type CandidateStage = NonNullable<S['CandidateStage']>;
export type CandidateSource = NonNullable<S['CandidateSource']>;

export const OPENING_STATUS: Record<OpeningStatus, { label: string; tone: '' | 'positive' | 'negative' }> = {
  open: { label: 'Abierta', tone: '' },
  in_progress: { label: 'En proceso', tone: '' },
  filled: { label: 'Cubierta', tone: 'positive' },
  paused: { label: 'Pausada', tone: 'negative' },
  cancelled: { label: 'Cancelada', tone: 'negative' },
};

/** How the portal names each stage (SPEC §4.3: Entrevista reads «En entrevista»). */
export const STAGE: Record<CandidateStage, { label: string; tone: '' | 'positive' | 'negative' }> = {
  new: { label: 'Nuevo', tone: '' },
  contacted: { label: 'Contactado', tone: '' },
  interview: { label: 'En entrevista', tone: '' },
  suitable: { label: 'Apto', tone: 'positive' },
  not_suitable: { label: 'No apto', tone: 'negative' },
  hired: { label: 'Contratado', tone: 'positive' },
  discarded: { label: 'Descartado', tone: 'negative' },
};

/** The card's mini funnel (prototype): Nuevo · Contactado · Entrevista · Apto · Contratado. */
export const FUNNEL: readonly { stage: CandidateStage; label: string }[] = [
  { stage: 'new', label: 'Nuevo' },
  { stage: 'contacted', label: 'Contactado' },
  { stage: 'interview', label: 'Entrevista' },
  { stage: 'suitable', label: 'Apto' },
  { stage: 'hired', label: 'Contratado' },
];

export const SOURCE: Record<CandidateSource, string> = {
  facebook: 'Facebook',
  linkedin: 'LinkedIn',
  whatsapp: 'WhatsApp',
  referral: 'Referido',
  job_board: 'Bolsa de trabajo',
  other: 'Otro',
};

export function countOf(o: ClientOpeningDto, stage: CandidateStage): number {
  return Number(o.stageCounts.find((s) => s.stage === stage)?.count ?? 0);
}

/** The client's read-only portal (`/v1/client-portal`, PLAN D78); the API limits every call to the user's client. */
@Injectable({ providedIn: 'root' })
export class ClientPortal {
  private readonly api = inject(API_CLIENT);

  openings() {
    return liveQuery(() => this.api.get<ClientOpeningDto[]>('/v1/client-portal/openings'), ['client-portal', 'openings', 'candidates']);
  }

  opening(id: () => string) {
    return liveQuery(
      () => this.api.get<ClientOpeningDetailDto>(`/v1/client-portal/openings/${id()}`),
      ['client-portal', 'openings', 'candidates', 'documents'],
    );
  }

  comments(id: () => string) {
    return liveQuery(() => this.api.get<ClientCommentDto[]>(`/v1/client-portal/openings/${id()}/comments`), ['client-portal', 'openings']);
  }

  feed() {
    return liveQuery(() => this.api.get<ClientCommentDto[]>('/v1/client-portal/comments'), ['client-portal', 'openings']);
  }

  post(id: string, body: string): Promise<ClientCommentDto> {
    return this.api.post(`/v1/client-portal/openings/${id}/comments`, { body });
  }
}
