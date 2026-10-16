/**
 * Wire primitives shared by every generated type.
 *
 * - `Decimal`: money, rates and hours travel as decimal strings ("1234.56"); the TypeScript side never parses them
 *   into a number (PLAN D46). Format with `formatMoney`, compare with `compareDecimal`.
 * - `DateKey`: a business date "yyyy-MM-dd", computed on the server in the payroll group's time zone. Never turned
 *   into a `Date` (that would shift it by the browser's offset).
 */
export type Decimal = string & { readonly __decimal?: never };
export type DateKey = string & { readonly __dateKey?: never };

const DECIMAL = /^-?\d+(\.\d+)?$/;
const DATE_KEY = /^\d{4}-\d{2}-\d{2}$/;

export const isDecimal = (value: unknown): value is Decimal => typeof value === 'string' && DECIMAL.test(value);
export const isDateKey = (value: unknown): value is DateKey => typeof value === 'string' && DATE_KEY.test(value);
