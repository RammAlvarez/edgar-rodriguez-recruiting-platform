// Pure mapping of the client's workbooks for scripts/import-excel.mjs (DATA_MODEL §10, PLAN D90). Nothing here prints an
// account number: callers report accounts by bank and last four digits only.

/** Bank names as the workbook's COMENTARIOS write them → SPEI code. */
export const BANK_NAMES = [
  ['BANCO AZTECA', '127'], ['AZTECA', '127'], ['BBVA', '012'], ['BANCOMER', '012'], ['BANORTE', '072'], ['BANAMEX', '002'],
  ['CITIBANAMEX', '002'], ['SANTANDER', '014'], ['BANREGIO', '058'], ['HSBC', '021'], ['SCOTIABANK', '044'], ['INBURSA', '036'],
  ['BANCOPPEL', '137'], ['COPPEL', '137'], ['SPIN', '728'], ['NU', '638'],
];

/** «Pérez  López, Ana» → «ANA LOPEZ PEREZ»: the same person in any word order, case or accents. */
export function nameKey(name) {
  return (name ?? '')
    .normalize('NFD').replace(/[̀-ͯ]/g, '').toUpperCase().replace(/[^A-Z\s]/g, ' ')
    .split(/\s+/).filter(Boolean).sort().join(' ');
}

function bankIn(text) {
  const upper = (text ?? '').toUpperCase();
  return BANK_NAMES.find(([name]) => new RegExp(`(^|[^A-Z])${name}([^A-Z]|$)`).test(upper))?.[1] ?? null;
}

/**
 * The accounts a COMENTARIOS cell names («BANORTE 4152 3138 …» / «CLABE 0125…»): each run of 10+ digits (spaces and dashes
 * allowed) with the bank written in its part (or anywhere in the cell, or a CLABE's own prefix) and its type by length —
 * 16 a card, 18 a CLABE, 14 or 20 an Azteca account. A number that fits none is returned with `type: null` for the report.
 */
export function accountsFromComments(raw) {
  const cell = raw ?? '';
  const anyBank = bankIn(cell);
  const found = [];
  for (const part of cell.split('/')) {
    for (const match of part.matchAll(/\d(?:[\s-]?\d){9,}/g)) {
      const digits = match[0].replace(/\D/g, '');
      const type = digits.length === 16 ? 'debit_card' : digits.length === 18 ? 'clabe' : [14, 20].includes(digits.length) ? 'own_account' : null;
      let bankCode = bankIn(part) ?? anyBank;
      if (type === 'clabe' && !bankCode) bankCode = digits.slice(0, 3);
      if (type === 'own_account') bankCode = '127';
      found.push({ digits, type, bankCode, last4: digits.slice(-4) });
    }
  }
  return found;
}

/** BAJA / INCAPACIDAD in COMENTARIOS (DATA_MODEL §10): the employee's status after the week. */
export function statusFromComments(raw) {
  const upper = (raw ?? '').toUpperCase();
  if (/\bBAJA\b/.test(upper)) return 'terminated';
  if (/\bINCAPACIDAD\b/.test(upper)) return 'sick_leave';
  return null;
}

/**
 * A puesto's rates are the ones most of its rows carry (the header's row-2 rates are stale, DATA_MODEL §10); a row with other
 * rates keeps them as the employee's overrides.
 */
export function positionRates(rows) {
  const byPuesto = new Map();
  for (const r of rows) {
    const key = JSON.stringify([r.rates.pocket, r.rates.imss, r.rates.hourly]);
    const counts = byPuesto.get(r.puesto) ?? new Map();
    counts.set(key, (counts.get(key) ?? 0) + 1);
    byPuesto.set(r.puesto, counts);
  }
  return new Map([...byPuesto].map(([puesto, counts]) => {
    const [pocket, imss, hourly] = JSON.parse([...counts].sort((a, b) => b[1] - a[1])[0][0]);
    return [puesto, { pocket, imss, hourly }];
  }));
}

/** A value cut to cents (D42), as an integer number of cents; the epsilon keeps 285.71 from becoming 28570.999… */
export function cents(value) {
  const n = Number(value ?? 0);
  return Math.trunc(n * 100 + (n >= 0 ? 1e-6 : -1e-6));
}

/** «285.71» from 285.7133: cut, not rounded, as the grid shows it. */
export function shown(value) {
  return (cents(value) / 100).toFixed(2);
}

/** A cent or more apart once both are cut to cents: what the diff report lists. */
export function differs(a, b) {
  return cents(a) !== cents(b);
}

/** «•••• 4562» */
export function masked(last4) {
  return `•••• ${last4}`;
}
