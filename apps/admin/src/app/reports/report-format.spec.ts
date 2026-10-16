import { describe, expect, it } from 'vitest';
import type { DateKey, Decimal } from '@platform/shared';
import { average, delta, percent, presetRange, previousRange } from './report-format';

const day = (s: string) => s as DateKey;

describe('report format', () => {
  it('builds the presets as calendar ranges ending today (PLAN D80)', () => {
    expect(presetRange('month', day('2026-10-16'))).toEqual({ from: '2026-10-01', to: '2026-10-16' });
    expect(presetRange('quarter', day('2026-10-16'))).toEqual({ from: '2026-10-01', to: '2026-10-16' });
    expect(presetRange('quarter', day('2026-08-05'))).toEqual({ from: '2026-07-01', to: '2026-08-05' });
    expect(presetRange('year', day('2026-10-16'))).toEqual({ from: '2026-01-01', to: '2026-10-16' });
  });

  it('compares with the previous range of equal length, as the API does', () =>
    expect(previousRange(day('2026-08-17'), day('2026-10-16'))).toEqual({ from: '2026-06-17', to: '2026-08-16' }));

  it('shows the already-truncated figures without changing a digit', () => {
    expect(percent('0.66' as Decimal)).toBe('66 %');
    expect(percent('0.29' as Decimal)).toBe('29 %'); // 0.29 × 100 is 28.999… in floating point
    expect(percent(null)).toBe('—');
    expect(average('12' as Decimal)).toBe('12.0');
    expect(average(null)).toBe('—');
  });

  it('writes the Δ in units, points or days', () => {
    expect(delta(5, 3, 'count')).toEqual({ text: '+2 vs rango anterior', tone: 'up' });
    expect(delta('0.40', '0.45', 'rate')).toEqual({ text: '−5 pts vs rango anterior', tone: 'down' });
    expect(delta('30.0', '30.0', 'average')).toEqual({ text: '0.0 vs rango anterior', tone: '' });
    expect(delta('30.0', null, 'average')).toEqual({ text: 'sin comparación', tone: '' });
  });
});
