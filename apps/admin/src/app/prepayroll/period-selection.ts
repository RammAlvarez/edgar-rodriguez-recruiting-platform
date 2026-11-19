import { effect, untracked, type WritableSignal } from '@angular/core';
import type { Router } from '@angular/router';
import type { PayrollPeriodDto } from '../data/api-types';

export interface PeriodGroup {
  readonly key: string;
  readonly label: string;
  readonly periods: PayrollPeriodDto[];
}

/** Periods by client · group, in the order they arrive (newest first). */
export function groupPeriods(periods: readonly PayrollPeriodDto[] | null | undefined): PeriodGroup[] {
  const map = new Map<string, PeriodGroup>();
  for (const p of periods ?? []) {
    const g = map.get(p.groupId) ?? { key: p.groupId, label: `${p.clientName} · ${p.groupName}`, periods: [] };
    g.periods.push(p);
    map.set(p.groupId, g);
  }
  return [...map.values()];
}

/** What to open by default: the oldest period still open with lines (what needs work), else the newest. */
export function defaultPeriod(periods: readonly PayrollPeriodDto[]): PayrollPeriodDto | undefined {
  const open = periods.filter((p) => p.status !== 'paid' && Number(p.lines) > 0);
  return open.at(-1) ?? periods[0];
}

/**
 * Keeps `periodId` valid and in the URL for a page with a period picker (Prenómina, Incidencias): picks the default when the one
 * in the URL is not among the periods, writes the choice to `?period=`, and reloads the page's data when it changes after the
 * first run. Call from a constructor (it creates effects).
 */
export function bindPeriodSelection(options: {
  periods: () => readonly PayrollPeriodDto[] | null | undefined;
  periodId: WritableSignal<string | null>;
  router: Router;
  reload: () => void;
}): void {
  const { periods, periodId, router, reload } = options;
  effect(() => {
    const all = periods();
    if (!all?.length || all.some((p) => p.id === untracked(periodId))) return;
    periodId.set(defaultPeriod(all)!.id);
  });
  let first = true;
  effect(() => {
    const period = periodId();
    void router.navigate([], { queryParams: { period }, replaceUrl: true });
    if (first) {
      first = false;
      return;
    }
    untracked(reload);
  });
}
