import type { AttendanceStatus } from '../data/api-types';

/**
 * What a supervisor captures (SPEC §5.3). Vacaciones and incapacidad are not here: they come from «Cambiar estatus» in
 * Personal and arrive already filled in.
 */
export const CAPTURE_STATUSES: readonly AttendanceStatus[] = [
  'present',
  'partial',
  'absent',
  'rest_day',
  'rest_day_worked',
  'holiday',
  'holiday_worked',
  'leave_paid',
  'leave_unpaid',
];

/** The one-tap chips; the rest are under «Más». */
export const QUICK_STATUSES: readonly AttendanceStatus[] = ['present', 'partial', 'absent', 'rest_day'];

/** Statuses that carry hours. */
export const WORKED: readonly AttendanceStatus[] = ['present', 'partial', 'rest_day_worked', 'holiday_worked'];

/** Short chip text for a phone screen. */
export const CHIP: Partial<Record<AttendanceStatus, string>> = {
  present: 'Presente',
  partial: 'Parcial',
  absent: 'Falta',
  rest_day: 'Descanso',
};

/** Icons of the chips (Material Symbols Outlined, as the prototype's capture buttons). */
export const CHIP_ICON: Partial<Record<AttendanceStatus, string>> = {
  present: 'check',
  partial: 'timelapse',
  absent: 'close',
  rest_day: 'bedtime',
};
