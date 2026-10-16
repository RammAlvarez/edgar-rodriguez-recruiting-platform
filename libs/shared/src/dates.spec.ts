import { describe, expect, it } from 'vitest';
import { addDaysToKey, formatDateKey, formatTimestamp, todayKey } from './dates';
import type { DateKey } from './wire';

describe('dates', () => {
  it('formats a business date without shifting it', () => expect(formatDateKey('2026-09-17' as DateKey)).toBe('17/09/2026'));

  it('formats timestamps in America/Monterrey', () =>
    // 07:43 UTC is 01:43 in Monterrey (UTC−6)
    expect(formatTimestamp(new Date('2026-10-16T07:43:57Z'))).toBe('16/10/2026 01:43'));

  it('takes today in Monterrey, not in UTC', () =>
    // 03:00 UTC on 17 October is still 16 October in Monterrey
    expect(todayKey(new Date('2026-10-17T03:00:00Z'))).toBe('2026-10-16'));

  it('adds days across months and years', () => {
    expect(addDaysToKey('2026-10-16' as DateKey, -90)).toBe('2026-07-18');
    expect(addDaysToKey('2026-12-31' as DateKey, 1)).toBe('2027-01-01');
  });
});
