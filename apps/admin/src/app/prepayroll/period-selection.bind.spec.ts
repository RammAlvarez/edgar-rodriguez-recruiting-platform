import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import type { PayrollPeriodDto } from '../data/api-types';
import { bindPeriodSelection } from './period-selection';

const period = (id: string, over: Partial<PayrollPeriodDto> = {}): PayrollPeriodDto =>
  ({ id, groupId: 'g', clientName: 'C', groupName: 'G', status: 'capturing', lines: 2, ...over }) as unknown as PayrollPeriodDto;

function setup(initialId: string | null, initialPeriods: PayrollPeriodDto[] | undefined) {
  const periods = signal<PayrollPeriodDto[] | undefined>(initialPeriods);
  const periodId = signal<string | null>(initialId);
  const navigate = vi.fn(async () => true);
  const reload = vi.fn();
  TestBed.runInInjectionContext(() => bindPeriodSelection({ periods, periodId, router: { navigate } as unknown as Router, reload }));
  TestBed.tick();
  return { periods, periodId, navigate, reload };
}

describe('bindPeriodSelection', () => {
  it('opens the default period when the one in the URL is not among the periods, and writes it to ?period=', () => {
    const { periodId, navigate } = setup('gone', [period('new'), period('old')]);
    TestBed.tick();
    expect(periodId()).toBe('old'); // oldest still open
    expect(navigate).toHaveBeenLastCalledWith([], { queryParams: { period: 'old' }, replaceUrl: true });
  });

  it('keeps a period that exists and does not reload the data on the first run', () => {
    const { periodId, reload } = setup('new', [period('new'), period('old')]);
    expect(periodId()).toBe('new');
    expect(reload).not.toHaveBeenCalled();
  });

  it('reloads when the user picks another period', () => {
    const { periodId, reload, navigate } = setup('new', [period('new'), period('old')]);
    periodId.set('old');
    TestBed.tick();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenLastCalledWith([], { queryParams: { period: 'old' }, replaceUrl: true });
  });

  it('waits for the periods before choosing', () => {
    const { periods, periodId } = setup(null, undefined);
    expect(periodId()).toBeNull();
    periods.set([period('a', { status: 'paid' }), period('b')]);
    TestBed.tick();
    expect(periodId()).toBe('b');
  });
});
