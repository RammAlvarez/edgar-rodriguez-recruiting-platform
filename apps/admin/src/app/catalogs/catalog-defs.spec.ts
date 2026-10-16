import { describe, expect, it } from 'vitest';
import { CATALOGS, toRequest } from './catalog-defs';

describe('catalog panels', () => {
  it('turns form values into the request body of the API', () => {
    const types = CATALOGS.find((c) => c.key === 'documentTypes')!;
    expect(
      toRequest(types, { code: ' LICENCIA ', nameEs: 'Licencia', hasExpiry: true, defaultValidityDays: '', sortOrder: '5', isActive: true }),
    ).toEqual({ code: 'LICENCIA', nameEs: 'Licencia', hasExpiry: true, defaultValidityDays: null, sortOrder: 5, isActive: true });
  });

  it('only holidays are deleted instead of deactivated', () => {
    expect(CATALOGS.filter((c) => !c.hasActive).map((c) => c.key)).toEqual(['holidays']);
  });
});
