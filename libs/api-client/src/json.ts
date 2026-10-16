/**
 * ISO-8601 timestamps with a time and an offset ("2026-10-16T07:43:57.123Z", "…-06:00") become `Date`; everything else —
 * including business dates "yyyy-MM-dd" and decimal strings — stays a string (PLAN D55, matching the generated types).
 */
const TIMESTAMP = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/;

export function isoDateReviver(_key: string, value: unknown): unknown {
  return typeof value === 'string' && TIMESTAMP.test(value) ? new Date(value) : value;
}

export function parseJson<T>(text: string): T {
  return (text ? JSON.parse(text, isoDateReviver) : undefined) as T;
}
