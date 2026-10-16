import { describe, expect, it } from 'vitest';
import { weekdaysText } from './payroll-labels';

describe('weekdaysText', () => {
  it('reads a run, a list and every day', () => {
    expect(weekdaysText([1, 2, 3, 4, 5])).toBe('L–V');
    expect(weekdaysText([5, 1, 3])).toBe('L, Mi, V');
    expect(weekdaysText([1, 2, 3, 4, 5, 6, 7])).toBe('Diario');
    expect(weekdaysText([6, 7])).toBe('S, D');
  });
});
