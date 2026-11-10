import { describe, expect, it } from 'vitest';
import type { PayrollPeriodDto } from '../data/api-types';
import { defaultPeriod, groupPeriods } from './period-selection';

const period = (over: Partial<PayrollPeriodDto>): PayrollPeriodDto =>
  ({ id: 'p', groupId: 'g1', clientName: 'ECOVIA', groupName: 'VALIDADORES', status: 'capturing', lines: 3, ...over }) as unknown as PayrollPeriodDto;

describe('groupPeriods', () => {
  it('groups by payroll group, labelled «cliente · grupo», keeping the order they arrive in', () => {
    const groups = groupPeriods([
      period({ id: 'a', groupId: 'g1' }),
      period({ id: 'b', groupId: 'g2', clientName: 'HEB', groupName: 'LIMPIEZA' }),
      period({ id: 'c', groupId: 'g1' }),
    ]);
    expect(groups.map((g) => [g.label, g.periods.map((p) => p.id)])).toEqual([
      ['ECOVIA · VALIDADORES', ['a', 'c']],
      ['HEB · LIMPIEZA', ['b']],
    ]);
  });

  it('is empty without periods', () => {
    expect(groupPeriods(undefined)).toEqual([]);
    expect(groupPeriods(null)).toEqual([]);
  });
});

describe('defaultPeriod', () => {
  it('opens the oldest period still open with lines (the list is newest first)', () => {
    const periods = [period({ id: 'new' }), period({ id: 'mid' }), period({ id: 'old' }), period({ id: 'paid', status: 'paid' })];
    expect(defaultPeriod(periods)?.id).toBe('old');
  });

  it('skips paid periods and periods without lines, else falls back to the newest', () => {
    expect(defaultPeriod([period({ id: 'a', lines: 0 }), period({ id: 'b', status: 'paid' })])?.id).toBe('a');
    expect(defaultPeriod([])).toBeUndefined();
  });
});
