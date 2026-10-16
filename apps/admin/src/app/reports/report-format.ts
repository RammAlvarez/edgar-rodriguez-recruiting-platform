import { addDaysToKey, type DateKey, type Decimal } from '@platform/shared';
import type { RecruitingReport } from '../data/api-types';

/** The selector of SPEC §4.7, in order, with the prototype's icons. */
export const REPORTS: readonly { report: RecruitingReport; label: string; icon: string }[] = [
  { report: 'recruiter_performance', label: 'Rendimiento por reclutador', icon: 'support_agent' },
  { report: 'summary', label: 'Resumen de reclutamiento', icon: 'dashboard' },
  { report: 'open_openings', label: 'Vacantes abiertas y envejecimiento', icon: 'hourglass_top' },
  { report: 'stage_funnel', label: 'Embudo por etapa', icon: 'filter_alt' },
  { report: 'sources', label: 'Fuentes de reclutamiento', icon: 'hub' },
  { report: 'client_openings', label: 'Vacantes por cliente', icon: 'domain' },
];

export type RangePreset = 'month' | 'quarter' | 'year';

export const PRESETS: readonly { preset: RangePreset; label: string }[] = [
  { preset: 'month', label: 'Este mes' },
  { preset: 'quarter', label: 'Trimestre' },
  { preset: 'year', label: 'Año en curso' },
];

/** A preset as a range ending today: the current calendar month, quarter or year (PLAN D80). */
export function presetRange(preset: RangePreset, today: DateKey): { from: DateKey; to: DateKey } {
  const [y, m] = today.split('-').map(Number);
  const month = preset === 'year' ? 1 : preset === 'quarter' ? Math.floor((m - 1) / 3) * 3 + 1 : m;
  return { from: `${y}-${String(month).padStart(2, '0')}-01` as DateKey, to: today };
}

/** The range before, of equal length — what the API compares the Resumen with. */
export function previousRange(from: DateKey, to: DateKey): { from: DateKey; to: DateKey } {
  const days = (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1;
  const previousTo = addDaysToKey(from, -1);
  return { from: addDaysToKey(previousTo, 1 - days), to: previousTo };
}

// The API sends rates and averages already truncated (0.66, 12.5 — PLAN D42), so formatting never changes a digit and the
// screen shows what the Excel and the PDF show.

/** 0.66 → «66 %»; null → «—». */
export function percent(value: Decimal | null | undefined): string {
  return value == null ? '—' : `${Math.round(Number(value) * 100)} %`;
}

/** 12.5 → «12.5»; null → «—». */
export function average(value: Decimal | null | undefined): string {
  return value == null ? '—' : Number(value).toFixed(1);
}

/** Integers travel as `number | string` in the generated types. */
export function count(value: number | string | null | undefined): string {
  return value == null ? '—' : String(value);
}

/** The Δ line of a Resumen tile: «+2 vs rango anterior», «−5 pts vs rango anterior» (rates in points). */
export function delta(current: number | string | null, previous: number | string | null, kind: 'count' | 'rate' | 'average'): {
  text: string;
  tone: '' | 'up' | 'down';
} {
  if (current == null || previous == null) return { text: 'sin comparación', tone: '' };
  const scale = kind === 'rate' ? 100 : kind === 'average' ? 10 : 1;
  const d = Math.round(Number(current) * scale) - Math.round(Number(previous) * scale);
  const shown = kind === 'average' ? (Math.abs(d) / 10).toFixed(1) : String(Math.abs(d));
  const sign = d > 0 ? '+' : d < 0 ? '−' : '';
  return { text: `${sign}${shown}${kind === 'rate' ? ' pts' : ''} vs rango anterior`, tone: d > 0 ? 'up' : d < 0 ? 'down' : '' };
}
