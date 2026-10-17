import type { PayrollReport } from '../data/api-types';

/** The selector of SPEC §5.9, in order. */
export const PAYROLL_REPORTS: readonly { report: PayrollReport; label: string; icon: string }[] = [
  { report: 'cost', label: 'Costo de nómina por cliente', icon: 'payments' },
  { report: 'payments', label: 'Resumen de pago', icon: 'account_balance' },
  { report: 'incidents', label: 'Incidencias del período', icon: 'edit_note' },
  { report: 'attendance', label: 'Asistencia por empleado', icon: 'fact_check' },
  { report: 'turnover', label: 'Rotación', icon: 'sync_alt' },
  { report: 'accounts', label: 'Tarjetas y cuentas', icon: 'credit_card' },
];

/** The date control each report takes (SPEC §5.9). */
export const DATE_MODE: Record<PayrollReport, 'week-or-range' | 'week' | 'months' | 'none'> = {
  cost: 'week-or-range',
  payments: 'week',
  incidents: 'week-or-range',
  attendance: 'week-or-range',
  turnover: 'months',
  accounts: 'none',
};

export type PayrollPreset = 'weeks' | 'month' | 'year';

export const PAYROLL_PRESETS: readonly { preset: PayrollPreset; label: string }[] = [
  { preset: 'weeks', label: 'Últimas 4 semanas' },
  { preset: 'month', label: 'Este mes' },
  { preset: 'year', label: 'Año en curso' },
];

const key = (d: Date) => d.toISOString().slice(0, 10);

/** A preset as a range ending today: the last 28 days, the calendar month or the calendar year. */
export function payrollPreset(preset: PayrollPreset, today: string): { from: string; to: string } {
  if (preset === 'weeks') {
    const d = new Date(`${today}T12:00:00Z`);
    d.setUTCDate(d.getUTCDate() - 27);
    return { from: key(d), to: today };
  }
  return { from: preset === 'month' ? `${today.slice(0, 7)}-01` : `${today.slice(0, 4)}-01-01`, to: today };
}

/** «2026-09-17» → «2026-09-01». */
export function monthStart(date: string): string {
  return `${date.slice(0, 7)}-01`;
}

/** «2026-02-10» → «2026-02-28»: the last day of the month. */
export function monthEnd(date: string): string {
  const [y, m] = date.split('-').map(Number);
  return key(new Date(Date.UTC(y, m, 0, 12)));
}
