import { type DateKey, isDateKey } from './wire';

export const DEFAULT_TIME_ZONE = 'America/Monterrey';

/** "2026-09-17" → "17/09/2026" (SPEC §8) without creating a Date. */
export function formatDateKey(value: DateKey | null | undefined): string {
  if (!value) return '';
  if (!isDateKey(value)) throw new Error(`Not a yyyy-MM-dd date: ${value}`);
  const [y, m, d] = value.split('-');
  return `${d}/${m}/${y}`;
}

/** A timestamp shown as "dd/MM/yyyy HH:mm" in the business time zone. */
export function formatTimestamp(value: Date | null | undefined, timeZone = DEFAULT_TIME_ZONE): string {
  if (!value) return '';
  const parts = new Intl.DateTimeFormat('es-MX', {
    timeZone,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(value);
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? '';
  return `${get('day')}/${get('month')}/${get('year')} ${get('hour')}:${get('minute')}`;
}
