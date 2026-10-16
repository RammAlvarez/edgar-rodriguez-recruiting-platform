import { describe, expect, it } from 'vitest';
import { formatDateKey, formatTimestamp } from './dates';
import type { DateKey } from './wire';

describe('dates', () => {
  it('formats a business date without shifting it', () => expect(formatDateKey('2026-09-17' as DateKey)).toBe('17/09/2026'));

  it('formats timestamps in America/Monterrey', () =>
    // 07:43 UTC is 01:43 in Monterrey (UTC−6)
    expect(formatTimestamp(new Date('2026-10-16T07:43:57Z'))).toBe('16/10/2026 01:43'));
});
