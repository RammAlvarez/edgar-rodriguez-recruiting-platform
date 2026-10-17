import { describe, expect, it } from 'vitest';
import { accountsFromComments, differs, nameKey, positionRates, shown, statusFromComments } from './import.mjs';

// Made-up numbers in the workbook's shapes (never the client's).
const CARD = '4152 3100 0000 0010';
const CLABE = '012580000000000011';

describe('import mappers (PLAN D90)', () => {
  it('reads accounts out of COMENTARIOS with their bank and type', () => {
    expect(accountsFromComments(`BANORTE ${CARD}`)).toEqual([{ digits: '4152310000000010', type: 'debit_card', bankCode: '072', last4: '0010' }]);
    expect(accountsFromComments(`AGREGAR PRIMA / ${CLABE}`)).toEqual([{ digits: CLABE, type: 'clabe', bankCode: '012', last4: '0011' }]);
    expect(accountsFromComments('AZTECA 12345678901234')[0]).toMatchObject({ type: 'own_account', bankCode: '127' });
    expect(accountsFromComments('CUENTA 123456789012')[0]).toMatchObject({ type: null });
    expect(accountsFromComments('EXTRANJERO')).toEqual([]);
    expect(accountsFromComments(null)).toEqual([]);
  });

  it('takes BAJA and INCAPACIDAD as statuses', () => {
    expect(statusFromComments('BAJA')).toBe('terminated');
    expect(statusFromComments('incapacidad / imss')).toBe('sick_leave');
    expect(statusFromComments('BAJANTE')).toBeNull();
  });

  it('gives each puesto the rates most of its rows carry', () => {
    const row = (puesto: string, pocket: number) => ({ puesto, rates: { pocket, imss: 278.8, hourly: pocket / 12 } });
    const rates = positionRates([row('VALIDADOR', 428.57), row('VALIDADOR', 428.57), row('VALIDADOR', 585.71), row('SUPERVISOR', 585.71)]);
    expect(rates.get('VALIDADOR')!.pocket).toBe(428.57);
    expect(rates.get('SUPERVISOR')!.pocket).toBe(585.71);
  });

  it('matches names in any order, case or accents, and compares cents', () => {
    expect(nameKey('Pérez López, Ana')).toBe(nameKey('ANA LOPEZ PEREZ'));
    expect(differs('428.52', 428.5299)).toBe(false);
    expect(differs('428.52', 428.5)).toBe(true);
    expect(differs('285.71', 285.71333)).toBe(false);
    expect(differs('146.42', 146.4275)).toBe(false);
    expect(shown(146.4275)).toBe('146.42');
    expect(shown(-0.005)).toBe('0.00');
  });
});
