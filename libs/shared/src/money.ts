import { type Decimal, isDecimal } from './wire';

/**
 * "$ 1,234.56" (SPEC §8, es-MX with a space after the sign). Display truncates to cents — never rounds (PLAN D42) —
 * and works on the string digits only, so no binary float ever touches the amount.
 */
export function formatMoney(value: Decimal | null | undefined, options: { currency?: boolean } = {}): string {
  if (value === null || value === undefined || value === '') return '';
  if (!isDecimal(value)) throw new Error(`Not a decimal string: ${value}`);
  const negative = value.startsWith('-');
  const [intPart, fraction = ''] = (negative ? value.slice(1) : value).split('.');
  const cents = (fraction + '00').slice(0, 2);
  const grouped = intPart.replace(/^0+(?=\d)/, '').replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  const isZero = /^0*$/.test(intPart + cents);
  const sign = negative && !isZero ? '-' : '';
  return `${sign}$ ${grouped}.${cents}${options.currency ? ' MXN' : ''}`;
}

/** Truncates a decimal string to `digits` decimals (1234.5678 → "1234.56"), the cents rule of PLAN D42. */
export function truncateDecimal(value: Decimal, digits = 2): Decimal {
  if (!isDecimal(value)) throw new Error(`Not a decimal string: ${value}`);
  const [intPart, fraction = ''] = value.split('.');
  return (digits === 0 ? intPart : `${intPart}.${(fraction + '0'.repeat(digits)).slice(0, digits)}`) as Decimal;
}

/** Orders two decimal strings without converting them to numbers. */
export function compareDecimal(a: Decimal, b: Decimal): -1 | 0 | 1 {
  const [sa, sb] = [a.startsWith('-'), b.startsWith('-')];
  const abs = (v: string) => (v.startsWith('-') ? v.slice(1) : v);
  const norm = (v: string) => {
    const [i, f = ''] = abs(v).split('.');
    return [i.replace(/^0+(?=\d)/, ''), f.replace(/0+$/, '')] as const;
  };
  const cmpAbs = () => {
    const [ia, fa] = norm(a);
    const [ib, fb] = norm(b);
    if (ia.length !== ib.length) return ia.length > ib.length ? 1 : -1;
    if (ia !== ib) return ia > ib ? 1 : -1;
    const len = Math.max(fa.length, fb.length);
    const [pa, pb] = [fa.padEnd(len, '0'), fb.padEnd(len, '0')];
    return pa === pb ? 0 : pa > pb ? 1 : -1;
  };
  const isZero = (v: string) => /^-?0*(\.0*)?$/.test(v);
  if (isZero(a) && isZero(b)) return 0;
  if (sa !== sb) return sa ? -1 : 1;
  const r = cmpAbs();
  return (sa ? -r : r) as -1 | 0 | 1;
}
