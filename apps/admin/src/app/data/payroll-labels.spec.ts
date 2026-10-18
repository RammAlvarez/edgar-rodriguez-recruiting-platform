import { describe, expect, it } from 'vitest';
import { columnTitle, hourlyText, weekdaysText } from './payroll-labels';

describe('weekdaysText', () => {
  it('reads a run, a list and every day', () => {
    expect(weekdaysText([1, 2, 3, 4, 5])).toBe('L–V');
    expect(weekdaysText([5, 1, 3])).toBe('L, Mi, V');
    expect(weekdaysText([1, 2, 3, 4, 5, 6, 7])).toBe('Diario');
    expect(weekdaysText([6, 7])).toBe('S, D');
  });
});

describe('hourlyText', () => {
  it('keeps the significant decimals only', () => {
    expect(hourlyText('35.7100')).toBe('35.71');
    expect(hourlyText('35.7142')).toBe('35.7142');
    expect(hourlyText('40')).toBe('40.00');
    expect(hourlyText(null)).toBe('—');
  });
});

describe('columnTitle', () => {
  const partial = 'MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS';

  it('names the turno of the group in the partial-hours column only', () => {
    expect(columnTitle(partial, '8.00')).toBe('MONTO DE HORAS INCOMPLETAS SOBRE 8 HORAS');
    expect(columnTitle(partial, '12.00')).toBe(partial);
    expect(columnTitle('SUELDO', '8.00')).toBe('SUELDO');
  });
});
