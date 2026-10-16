import type { AttendanceStatus, BankAccountType, EmployeeStatus, PeriodType, RegistrationBank } from './api-types';

/** Status pill text and tone (design system): Activo neutral-positive, Baja negative, the rest neutral. */
export const EMPLOYEE_STATUS: Record<EmployeeStatus, { label: string; tone: '' | 'positive' | 'negative' }> = {
  active: { label: 'Activo', tone: 'positive' },
  vacation: { label: 'Vacaciones', tone: '' },
  sick_leave: { label: 'Incapacidad', tone: '' },
  suspended: { label: 'Suspendido', tone: 'negative' },
  terminated: { label: 'Baja', tone: 'negative' },
};

export const PERIOD_STATUS_LABEL: Record<EmployeeStatus, string> = {
  active: 'Activo',
  vacation: 'Vacaciones',
  sick_leave: 'Incapacidad',
  suspended: 'Suspensión',
  terminated: 'Baja',
};

export const REGISTRATION_BANK: Record<RegistrationBank, string> = { banorte: 'Banorte', banregio: 'Banregio' };

export const ACCOUNT_TYPES: readonly { value: BankAccountType; label: string; hint: string }[] = [
  { value: 'debit_card', label: 'Tarjeta de débito', hint: '16 dígitos' },
  { value: 'clabe', label: 'CLABE', hint: '18 dígitos' },
  { value: 'own_account', label: 'Cuenta propia', hint: '14 o 20 dígitos' },
];
export const ACCOUNT_TYPE: Record<BankAccountType, string> = Object.fromEntries(ACCOUNT_TYPES.map((t) => [t.value, t.label])) as Record<
  BankAccountType,
  string
>;

export const PERIOD_TYPE: Record<PeriodType, string> = { weekly: 'Semanal', biweekly: 'Quincenal' };

/** 0 = domingo … 6 = sábado (`payroll_groups.cutoff_weekday`). */
export const WEEKDAYS_FROM_SUNDAY = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'] as const;

/** ISO 1 = lunes … 7 = domingo (`work_schedules.weekdays`). */
export const ISO_WEEKDAYS: readonly { value: number; short: string; label: string }[] = [
  { value: 1, short: 'L', label: 'Lunes' },
  { value: 2, short: 'M', label: 'Martes' },
  { value: 3, short: 'Mi', label: 'Miércoles' },
  { value: 4, short: 'J', label: 'Jueves' },
  { value: 5, short: 'V', label: 'Viernes' },
  { value: 6, short: 'S', label: 'Sábado' },
  { value: 7, short: 'D', label: 'Domingo' },
];

/** «L–V», «L, Mi, V», «Diario». */
export function weekdaysText(days: readonly number[]): string {
  const sorted = [...days].sort((a, b) => a - b);
  if (sorted.length === 7) return 'Diario';
  const isRun = sorted.length > 2 && sorted.every((d, i) => i === 0 || d === sorted[i - 1] + 1);
  const short = (d: number) => ISO_WEEKDAYS.find((w) => w.value === d)!.short;
  return isRun ? `${short(sorted[0])}–${short(sorted[sorted.length - 1])}` : sorted.map(short).join(', ');
}

/** The attendance a calendar entry already has (S8 adds capture; S7 writes only status periods). */
export const ATTENDANCE: Record<AttendanceStatus, { label: string; tone: '' | 'positive' | 'negative' }> = {
  present: { label: 'Presente', tone: 'positive' },
  partial: { label: 'Parcial', tone: '' },
  absent: { label: 'Falta', tone: 'negative' },
  rest_day: { label: 'Descanso', tone: '' },
  rest_day_worked: { label: 'Descanso laborado', tone: 'positive' },
  holiday: { label: 'Festivo', tone: '' },
  holiday_worked: { label: 'Festivo laborado', tone: 'positive' },
  sick_leave: { label: 'Incapacidad', tone: '' },
  leave_paid: { label: 'Permiso con goce', tone: '' },
  leave_unpaid: { label: 'Suspensión', tone: 'negative' },
  vacation: { label: 'Vacaciones', tone: '' },
};

/** COSTO X HRA as typed: at least 2 decimals, up to the 4 stored, no trailing zeros (35.7100 → «35.71», 35.7142 → «35.7142»). */
export function hourlyText(value: string | number | null | undefined): string {
  if (value == null || value === '') return '—';
  const [int, frac = ''] = String(value).split('.');
  const trimmed = frac.replace(/0+$/, '');
  return `${int}.${trimmed.padEnd(2, '0')}`;
}

/** 12.00 → «12», 7.50 → «7.5». */
export function hoursText(value: string | number): string {
  return String(Number(value));
}
