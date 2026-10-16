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
import { z } from 'zod';
import type { ClientDetailDto } from '../data/api-types';
import { ClientsRepository } from '../data/repositories';
import { PanelState } from './panel-base';

export interface ClientPanelData {
  /** Which «Nuevo cliente» opened the panel; the API marks the module (SPEC §3.3), the panel never asks. */
  readonly module: 'recruiting' | 'payroll';
  /** Present when editing from the client detail. */
  readonly client?: ClientDetailDto;
}

const rfc = z
  .string()
  .trim()
  .toUpperCase()
  .regex(/^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$/, 'El RFC tiene 12 caracteres (persona moral) o 13 (persona física).');

/** «Nuevo cliente» of both modules (same fields, ARCHITECTURE §9.2) and «Editar cliente». */
@Component({
  selector: 'app-client-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.client ? 'Editar cliente' : 'Nuevo cliente' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="client-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre corto</mat-label>
          <input matInput formControlName="shortName" autocomplete="off" />
          <mat-hint>Como lo llamas a diario: ECOVIA, HEB</mat-hint>
          <mat-error>{{ s.msg('shortName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Razón social</mat-label>
          <input matInput formControlName="legalName" autocomplete="off" />
          <mat-error>{{ s.msg('legalName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>RFC</mat-label>
          <input matInput formControlName="rfc" autocomplete="off" class="mono" />
          <mat-error>{{ s.msg('rfc') }}</mat-error>
        </mat-form-field>
        @if (data.client) {
          <mat-form-field>
            <mat-label>Notas</mat-label>
            <textarea matInput formControlName="notes" rows="3"></textarea>
          </mat-form-field>
          <mat-slide-toggle formControlName="isActive">Activo</mat-slide-toggle>
        } @else {
          <mat-form-field>
            <mat-label>Contacto principal</mat-label>
            <input matInput formControlName="contactName" autocomplete="off" />
            <mat-error>{{ s.msg('primaryContact.fullName') ?? s.msg('contactName') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Correo</mat-label>
            <input matInput type="email" formControlName="contactEmail" autocomplete="off" />
            <mat-error>{{ s.msg('contactEmail') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Celular</mat-label>
            <input matInput type="tel" formControlName="contactPhone" autocomplete="off" />
            <mat-hint>10 dígitos</mat-hint>
            <mat-error>{{ s.msg('contactPhone') }}</mat-error>
          </mat-form-field>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="client-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
})
export class ClientPanel {
  readonly data = inject<ClientPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<ClientPanel, ClientDetailDto | true>);
  private readonly clients = inject(ClientsRepository);
  private readonly snack = inject(MatSnackBar);

  readonly form = inject(FormBuilder).nonNullable.group({
    shortName: [this.data.client?.shortName ?? '', [Validators.required, Validators.maxLength(40)]],
    legalName: [this.data.client?.legalName ?? '', Validators.maxLength(200)],
    rfc: [this.data.client?.rfc ?? '', zodValidator(rfc)],
    notes: [this.data.client?.notes ?? ''],
    isActive: [this.data.client?.isActive ?? true],
    contactName: ['', Validators.maxLength(120)],
    contactEmail: ['', zodValidator(email)],
    contactPhone: ['', zodValidator(mobilePhone)],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      if (this.data.client) {
        await this.clients.update(this.data.client.id, {
          shortName: v.shortName,
          legalName: text(v.legalName),
          rfc: text(v.rfc),
          notes: text(v.notes),
          isActive: v.isActive,
        });
        this.snack.open('Cliente actualizado', undefined, { duration: 4000 });
        this.ref.close(true);
        return;
      }
      const created = await this.clients.create(this.data.module, {
        shortName: v.shortName,
        legalName: text(v.legalName),
        rfc: text(v.rfc),
        primaryContact: text(v.contactName)
          ? { fullName: v.contactName.trim(), email: text(v.contactEmail), phone: text(v.contactPhone) }
          : null,
      });
      this.snack.open('Cliente creado', undefined, { duration: 4000 });
      this.ref.close(created);
    });
  }
}
