import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { email, mobilePhone } from '@platform/shared';
import { zodValidator } from '@platform/ui';
import type { ContactDto } from '../data/api-types';
import { ClientsRepository } from '../data/repositories';
import { PanelState } from './panel-base';

export interface ContactPanelData {
  readonly clientId: string;
  readonly contact: ContactDto | null;
}

@Component({
  selector: 'app-contact-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.contact ? 'Editar contacto' : 'Nuevo contacto' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="contact-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="fullName" autocomplete="off" />
          <mat-error>{{ s.msg('fullName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Puesto</mat-label>
          <input matInput formControlName="jobTitle" autocomplete="off" />
          <mat-error>{{ s.msg('jobTitle') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Correo</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="off" />
          <mat-error>{{ s.msg('email') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Celular</mat-label>
          <input matInput type="tel" formControlName="phone" autocomplete="off" />
          <mat-hint>10 dígitos</mat-hint>
          <mat-error>{{ s.msg('phone') }}</mat-error>
        </mat-form-field>
        <mat-slide-toggle formControlName="isPrimary">Contacto principal</mat-slide-toggle>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="contact-form" [disabled]="s.busy()">Guardar contacto</button>
    </mat-dialog-actions>
  `,
})
export class ContactPanel {
  readonly data = inject<ContactPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<ContactPanel>);
  private readonly clients = inject(ClientsRepository);
  private readonly snack = inject(MatSnackBar);

  readonly form = inject(FormBuilder).nonNullable.group({
    fullName: [this.data.contact?.fullName ?? '', [Validators.required, Validators.maxLength(120)]],
    jobTitle: [this.data.contact?.jobTitle ?? '', Validators.maxLength(80)],
    email: [this.data.contact?.email ?? '', zodValidator(email)],
    phone: [this.data.contact?.phone ?? '', zodValidator(mobilePhone)],
    isPrimary: [this.data.contact?.isPrimary ?? false],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      await this.clients.saveContact(this.data.clientId, this.data.contact?.id ?? null, {
        fullName: v.fullName.trim(),
        jobTitle: text(v.jobTitle),
        email: text(v.email),
        phone: text(v.phone),
        isPrimary: v.isPrimary,
      });
      this.snack.open('Contacto guardado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
