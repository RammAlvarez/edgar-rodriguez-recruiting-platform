import { describe, expect, it } from 'vitest';
import { DATE_MODE, monthEnd, monthStart, PAYROLL_REPORTS, payrollPreset } from './payroll-report-format';

describe('payroll report filters', () => {
  it('lists the six reports of SPEC §5.9, each with its date control', () => {
    expect(PAYROLL_REPORTS.map((r) => r.report)).toEqual(['cost', 'payments', 'incidents', 'attendance', 'turnover', 'accounts']);
    expect(PAYROLL_REPORTS.every((r) => DATE_MODE[r.report])).toBe(true);
    expect(DATE_MODE.accounts).toBe('none');
  });

  it('builds the presets as ranges ending today', () => {
    expect(payrollPreset('weeks', '2026-10-16')).toEqual({ from: '2026-09-19', to: '2026-10-16' });
    expect(payrollPreset('month', '2026-10-16')).toEqual({ from: '2026-10-01', to: '2026-10-16' });
    expect(payrollPreset('year', '2026-10-16')).toEqual({ from: '2026-01-01', to: '2026-10-16' });
  });

  it('takes whole months for Rotación', () => {
    expect(monthStart('2026-09-17')).toBe('2026-09-01');
    expect(monthEnd('2026-02-10')).toBe('2026-02-28');
    expect(monthEnd('2028-02-01')).toBe('2028-02-29');
    expect(monthEnd('2026-12-01')).toBe('2026-12-31');
  });
});
