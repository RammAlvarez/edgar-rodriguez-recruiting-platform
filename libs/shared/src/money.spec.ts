import { describe, expect, it } from 'vitest';
import { compareDecimal, formatMoney, truncateDecimal } from './money';
import type { Decimal } from './wire';

const d = (v: string) => v as Decimal;

describe('formatMoney', () => {
  it.each([
    ['1234.56', '$ 1,234.56'],
    ['1234.5678', '$ 1,234.56'], // truncated, never rounded (PLAN D42)
    ['1234.569', '$ 1,234.56'],
    ['0', '$ 0.00'],
    ['428.5', '$ 428.50'],
    ['333188.81', '$ 333,188.81'],
    ['-500', '-$ 500.00'],
    ['-0.004', '$ 0.00'],
    ['1000000', '$ 1,000,000.00'],
  ])('%s → %s', (value, expected) => expect(formatMoney(d(value))).toBe(expected));

  it('adds the currency on request', () => expect(formatMoney(d('1'), { currency: true })).toBe('$ 1.00 MXN'));
  it('renders empty for missing values', () => expect(formatMoney(null)).toBe(''));
  it('rejects non-decimal strings', () => expect(() => formatMoney(d('1e3'))).toThrow());
});

describe('truncateDecimal', () => {
  it.each([
    ['357.1416666666666666666666667', '357.14'],
    ['214.285', '214.28'],
    ['5', '5.00'],
    ['-1.999', '-1.99'],
  ])('%s → %s', (value, expected) => expect(truncateDecimal(d(value))).toBe(expected));
});

describe('compareDecimal', () => {
  it.each([
    ['1.5', '1.50', 0],
    ['10', '9.99', 1],
    ['-1', '0', -1],
    ['-2', '-1', -1],
    ['0.001', '0.0009', 1],
    ['-0', '0', 0],
  ] as const)('%s vs %s → %i', (a, b, expected) => expect(compareDecimal(d(a), d(b))).toBe(expected));
});
