import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Session } from '@platform/ui/session';
import { describe, expect, it } from 'vitest';
import { MfaRepository } from '../data/mfa-repository';
import { UsersRepository } from '../data/repositories';
import { UserPanel } from './user-panel';

const user = { id: 'u-1', displayName: 'Ana', email: 'ana@x.mx', role: 'administrator', clientId: null, mfaRequired: false, isActive: true, grants: [] };

function panel(target: typeof user, me: { id: string; mfaEnabled: boolean } | null) {
  TestBed.configureTestingModule({
    providers: [
      { provide: MAT_DIALOG_DATA, useValue: { user: target, clients: [], myId: me?.id ?? '' } },
      { provide: MatDialogRef, useValue: {} },
      { provide: Session, useValue: { me: signal(me) } },
      { provide: UsersRepository, useValue: {} },
      { provide: MfaRepository, useValue: {} },
      { provide: MatSnackBar, useValue: {} },
      { provide: MatDialog, useValue: {} },
    ],
  });
  return TestBed.runInInjectionContext(() => new UserPanel());
}

describe('UserPanel own-account warning', () => {
  it('shows only when requiring it for yourself while you have not set it up', () => {
    const own = panel(user, { id: 'u-1', mfaEnabled: false });
    expect(own.ownAccountRequiresMfa()).toBe(false);
    own.form.controls.mfaRequired.setValue(true);
    expect(own.ownAccountRequiresMfa()).toBe(true);
  });

  it('stays quiet for another user, or when you already have it on', () => {
    const other = panel(user, { id: 'u-2', mfaEnabled: false });
    other.form.controls.mfaRequired.setValue(true);
    expect(other.ownAccountRequiresMfa()).toBe(false);

    TestBed.resetTestingModule();
    const enrolled = panel(user, { id: 'u-1', mfaEnabled: true });
    enrolled.form.controls.mfaRequired.setValue(true);
    expect(enrolled.ownAccountRequiresMfa()).toBe(false);
  });
});
