import type { WritableSignal } from '@angular/core';

/**
 * Runs `work` once at a time behind a busy flag: a second click or Enter before the button shows as disabled is ignored
 * (D-024), a failure goes to `onError` (the caller decides between a toast, an alert or a field), and the flag always clears.
 * `onStart` clears whatever the previous attempt left on screen.
 */
export async function runExclusive(
  busy: WritableSignal<boolean>,
  work: () => Promise<void>,
  onError: (error: unknown) => void,
  onStart?: () => void,
): Promise<void> {
  if (busy()) return;
  busy.set(true);
  onStart?.();
  try {
    await work();
  } catch (e) {
    onError(e);
  } finally {
    busy.set(false);
  }
}
