import { describe, expect, it } from 'vitest';
import type { MeDto } from '@platform/shared';
import { mfaPending } from './mfa-guard';

const me = (over: Partial<MeDto>): MeDto =>
  ({ id: 'u', displayName: 'U', email: 'u@x', role: 'administrator', clientId: null, mfaRequired: false, mfaEnabled: false, mfaVerified: false,
    grants: [], actingFor: null, personId: null, ...over }) as MeDto;

describe('mfaPending', () => {
  it('asks staff who have it or must, until this sign-in passed the code', () => {
    expect(mfaPending(me({}))).toBe(false);
    expect(mfaPending(me({ mfaRequired: true }))).toBe(true);
    expect(mfaPending(me({ mfaEnabled: true }))).toBe(true);
    expect(mfaPending(me({ mfaEnabled: true, mfaVerified: true }))).toBe(false);
    expect(mfaPending(me({ role: 'recruiter', mfaEnabled: true }))).toBe(true);
  });

  it('never asks client users or candidates', () => {
    expect(mfaPending(me({ role: 'client', mfaRequired: true }))).toBe(false);
    expect(mfaPending(null)).toBe(false);
  });
});
