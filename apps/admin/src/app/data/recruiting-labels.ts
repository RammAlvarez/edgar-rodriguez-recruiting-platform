import type { components } from '@platform/shared';
import type { PillTone } from './pill-tone';

type S = components['schemas'];
// NonNullable: the OpenAPI document adds null to an enum's schema when some property using it is nullable.
export type OpeningStatus = NonNullable<S['OpeningStatus']>;
export type OpeningPriority = NonNullable<S['OpeningPriority']>;
export type CandidateStage = NonNullable<S['CandidateStage']>;
export type CandidateSource = NonNullable<S['CandidateSource']>;
export type InterviewOutcome = NonNullable<S['InterviewOutcome']>;
export type Gender = NonNullable<S['Gender']>;
export type OpeningEventKind = NonNullable<S['OpeningEventKind']>;

/** Pill text and tone: positive = done, negative = stopped, neutral otherwise (design system status pills). */
export const OPENING_STATUS: Record<OpeningStatus, { label: string; tone: PillTone }> = {
  open: { label: 'Abierta', tone: 'primary' },
  in_progress: { label: 'En proceso', tone: 'tonal' },
  filled: { label: 'Cubierta', tone: 'tertiary' },
  paused: { label: 'Pausada', tone: '' },
  cancelled: { label: 'Cancelada', tone: 'negative' },
};

export const PRIORITY: Record<OpeningPriority, string> = { high: 'Alta', medium: 'Media', low: 'Baja' };

/** Kanban column order (SPEC §4.3). */
export const STAGES: readonly CandidateStage[] = ['new', 'contacted', 'interview', 'suitable', 'not_suitable', 'hired', 'discarded'];

export const STAGE: Record<CandidateStage, { label: string; tone: PillTone }> = {
  new: { label: 'Nuevo', tone: '' },
  contacted: { label: 'Contactado', tone: 'tonal' },
  interview: { label: 'Entrevista', tone: 'tertiary' },
  suitable: { label: 'Apto', tone: 'positive' },
  not_suitable: { label: 'No apto', tone: 'negative' },
  hired: { label: 'Contratado', tone: 'primary' },
  discarded: { label: 'Descartado', tone: '' },
};

export const SOURCES: readonly { value: CandidateSource; label: string }[] = [
  { value: 'facebook', label: 'Facebook' },
  { value: 'linkedin', label: 'LinkedIn' },
  { value: 'whatsapp', label: 'WhatsApp' },
  { value: 'referral', label: 'Referido' },
  { value: 'job_board', label: 'Bolsa de trabajo' },
  { value: 'other', label: 'Otro' },
];
export const SOURCE: Record<CandidateSource, string> = Object.fromEntries(SOURCES.map((s) => [s.value, s.label])) as Record<
  CandidateSource,
  string
>;

export const OUTCOME: Record<InterviewOutcome, string> = { pending: 'Pendiente', attended: 'Asistió', no_show: 'No se presentó' };

export const GENDER: Record<Gender, string> = { unspecified: 'Indistinto', male: 'Masculino', female: 'Femenino' };
