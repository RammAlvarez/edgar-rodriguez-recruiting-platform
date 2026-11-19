import { signal } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { runExclusive } from './run-exclusive';

describe('runExclusive', () => {
  it('holds the busy flag while the work runs and clears it afterwards', async () => {
    const busy = signal(false);
    let seen = false;
    await runExclusive(busy, async () => void (seen = busy()), vi.fn());
    expect(seen).toBe(true);
    expect(busy()).toBe(false);
  });

  it('ignores a second call while one is running (a double click sends once)', async () => {
    const busy = signal(false);
    let release!: () => void;
    const work = vi.fn(() => new Promise<void>((resolve) => (release = resolve)));
    const first = runExclusive(busy, work, vi.fn());
    await runExclusive(busy, work, vi.fn());
    expect(work).toHaveBeenCalledTimes(1);
    release();
    await first;
    await runExclusive(busy, async () => undefined, vi.fn());
    expect(busy()).toBe(false);
  });

  it('hands a failure to onError, clears the flag and runs onStart first', async () => {
    const busy = signal(false);
    const order: string[] = [];
    const boom = new Error('boom');
    await runExclusive(
      busy,
      async () => {
        order.push('work');
        throw boom;
      },
      (e) => order.push(e === boom ? 'error' : 'other'),
      () => order.push('start'),
    );
    expect(order).toEqual(['start', 'work', 'error']);
    expect(busy()).toBe(false);
  });
});
