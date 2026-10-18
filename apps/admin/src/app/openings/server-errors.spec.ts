import { FormControl, FormGroup } from '@angular/forms';
import { ApiError } from '@platform/api-client';
import { applyServerErrors, errorMessage, PanelState } from '@platform/ui';
import { describe, expect, it, vi } from 'vitest';

// FORMS run (D-022): «Hasta 30 prestaciones» lands on Prestaciones while the fix is in Otras prestaciones; the mark
// stayed, the form stayed invalid and every later Guardar did nothing.
describe('applyServerErrors', () => {
  const form = () => new FormGroup({ benefits: new FormControl<string[]>([]), otherBenefits: new FormControl('') });

  it('shows the message on its control and returns the ones that match none', () => {
    const f = form();
    const left = applyServerErrors(f, new ApiError(400, 'x', { benefits: ['Hasta 30 prestaciones.'], other: ['Otro.'] }));
    expect(errorMessage(f.get('benefits'))).toBe('Hasta 30 prestaciones.');
    expect(left).toEqual(['Otro.']);
    expect(f.invalid).toBe(true);
  });

  it('clears the marks when another field changes, so the form can be sent again', () => {
    const f = form();
    applyServerErrors(f, new ApiError(400, 'x', { benefits: ['Hasta 30 prestaciones.'] }));
    f.controls.otherBenefits.setValue('Bono');
    expect(errorMessage(f.get('benefits'))).toBeNull();
    expect(f.valid).toBe(true);
  });
});

// FORMS run (D-024): two clicks before the button rendered as disabled sent the form twice (two vacantes).
describe('PanelState.run', () => {
  it('sends once while a save is in flight', async () => {
    const s = new PanelState(new FormGroup({ name: new FormControl('x') }));
    let finish!: () => void;
    const work = vi.fn(() => new Promise<void>((r) => (finish = r)));
    const first = s.run(work);
    await s.run(work);
    finish();
    await first;
    expect(work).toHaveBeenCalledOnce();
  });
});
