import { describe, expect, it } from 'vitest';
import { fromGrants, toGrants } from './grants';

describe('grant checkboxes ↔ API grants', () => {
  it('puts the client scope on payroll grants only, never on Usuarios or Reportes', () => {
    expect(toGrants(['payroll_prepayroll', 'users', 'recruiting_reports'], 'c1')).toEqual([
      { module: 'payroll_prepayroll', clientId: 'c1' },
      { module: 'users', clientId: null },
      { module: 'recruiting_reports', clientId: null },
    ]);
  });

  it('reads back one scope, or flags mixed scopes', () => {
    expect(fromGrants(toGrants(['payroll_staff', 'users'], 'c1'))).toEqual({ modules: ['payroll_staff', 'users'], onlyClientId: 'c1', mixed: false });
    expect(
      fromGrants([
        { module: 'payroll_staff', clientId: 'c1' },
        { module: 'payroll_incidents', clientId: null },
      ]).mixed,
    ).toBe(true);
  });
});
