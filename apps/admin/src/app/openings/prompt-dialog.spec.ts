import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { describe, expect, it, vi } from 'vitest';
import { PromptDialog, type PromptData } from './prompt-dialog';

function render(data: PromptData) {
  const close = vi.fn();
  TestBed.configureTestingModule({
    imports: [PromptDialog],
    providers: [
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: { close } },
    ],
  });
  const fixture = TestBed.createComponent(PromptDialog);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const submit = el.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  return { fixture, close, submit };
}

// HAPPY run (D-017): the form had (ngSubmit) but no form directive, so «Marcar como contratado» reloaded the page.
describe('PromptDialog', () => {
  it('submits through Angular and closes with the fecha de ingreso', () => {
    const { close, submit } = render({ title: 'Contratar', label: 'Fecha de ingreso', confirm: 'Marcar como contratado', kind: 'date', initial: '2026-10-17' });
    const native = vi.fn((e: Event) => expect(e.defaultPrevented).toBe(true));
    submit.form!.addEventListener('submit', native);
    submit.click();
    expect(native).toHaveBeenCalledOnce();
    expect(close).toHaveBeenCalledWith('2026-10-17');
  });

  it('keeps the dialog open while the motivo is empty', () => {
    const { close, submit } = render({ title: 'No apto', label: 'Motivo', confirm: 'Marcar como no apto', kind: 'text' });
    submit.click();
    expect(close).not.toHaveBeenCalled();
  });
});
