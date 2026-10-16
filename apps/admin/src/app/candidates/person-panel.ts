import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { curp, email, mobilePhone, rfcPerson } from '@platform/shared';
import { API_CLIENT, zodValidator } from '@platform/ui';
import { z } from 'zod';
import type { PersonDto, PersonRequest } from '../data/api-types';
import { GENDER, SOURCES, type CandidateSource, type Gender } from '../data/recruiting-labels';
import { PanelState } from '../clients/panel-base';

export interface PersonPanelData {
  readonly person: PersonDto;
}

const digits = (n: number, message: string) => z.string().trim().regex(new RegExp(`^\\d{${n}}$`), message);

/** «Editar datos personales» of the candidate detail (SPEC §3.1): identity, contact and address; names are stored uppercase. */
@Component({
  selector: 'app-person-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Datos personales</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="person-form" novalidate>
        <h3 class="section-title">Identidad</h3>
        @for (f of identity; track f.name) {
          <mat-form-field>
            <mat-label>{{ f.label }}</mat-label>
            <input matInput [formControlName]="f.name" [type]="f.type ?? 'text'" [class.mono]="f.mono" autocomplete="off" />
            @if (f.hint) {
              <mat-hint>{{ f.hint }}</mat-hint>
            }
            <mat-error>{{ s.msg(f.name) }}</mat-error>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Sexo</mat-label>
          <mat-select formControlName="gender">
            @for (g of genders; track g.value) {
              <mat-option [value]="g.value">{{ g.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-slide-toggle formControlName="isForeign">Extranjero</mat-slide-toggle>
        <h3 class="section-title">Contacto y domicilio</h3>
        @for (f of contact; track f.name) {
          <mat-form-field>
            <mat-label>{{ f.label }}</mat-label>
            <input matInput [formControlName]="f.name" [type]="f.type ?? 'text'" autocomplete="off" />
            @if (f.hint) {
              <mat-hint>{{ f.hint }}</mat-hint>
            }
            <mat-error>{{ s.msg(f.name) }}</mat-error>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Fuente</mat-label>
          <mat-select formControlName="source">
            <mat-option [value]="null">—</mat-option>
            @for (src of sources; track src.value) {
              <mat-option [value]="src.value">{{ src.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Observaciones</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="person-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-slide-toggle {
      margin: 4px 0 12px;
    }
  `,
})
export class PersonPanel {
  readonly data = inject<PersonPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<PersonPanel, boolean>);
  private readonly api = inject(API_CLIENT);
  private readonly snack = inject(MatSnackBar);

  readonly genders = (Object.keys(GENDER) as Gender[]).map((value) => ({ value, label: GENDER[value] }));
  readonly sources = SOURCES;
  readonly identity: readonly { name: string; label: string; type?: string; hint?: string; mono?: boolean }[] = [
    { name: 'firstNames', label: 'Nombre(s)' },
    { name: 'lastNamePaternal', label: 'Apellido paterno' },
    { name: 'lastNameMaternal', label: 'Apellido materno' },
    { name: 'curp', label: 'CURP', mono: true, hint: 'La fecha de nacimiento y el sexo se toman de la CURP' },
    { name: 'rfc', label: 'RFC', mono: true },
    { name: 'nss', label: 'NSS', mono: true, hint: '11 dígitos' },
    { name: 'birthDate', label: 'Fecha de nacimiento', type: 'date' },
    { name: 'birthState', label: 'Estado de nacimiento' },
  ];
  readonly contact: readonly { name: string; label: string; type?: string; hint?: string }[] = [
    { name: 'mobilePhone', label: 'Celular', type: 'tel', hint: '10 dígitos' },
    { name: 'email', label: 'Correo', type: 'email' },
    { name: 'street', label: 'Calle' },
    { name: 'extNumber', label: 'Número' },
    { name: 'neighborhood', label: 'Colonia' },
    { name: 'postalCode', label: 'Código postal' },
    { name: 'municipality', label: 'Municipio' },
    { name: 'state', label: 'Estado' },
  ];

  private readonly p = this.data.person;
  readonly form = inject(FormBuilder).nonNullable.group({
    firstNames: [this.p.firstNames, [Validators.required, Validators.maxLength(80)]],
    lastNamePaternal: [this.p.lastNamePaternal, [Validators.required, Validators.maxLength(60)]],
    lastNameMaternal: [this.p.lastNameMaternal ?? ''],
    curp: [this.p.curp ?? '', zodValidator(curp)],
    rfc: [this.p.rfc ?? '', zodValidator(rfcPerson)],
    nss: [this.p.nss ?? '', zodValidator(digits(11, 'El NSS tiene 11 dígitos.'))],
    birthDate: [this.p.birthDate ?? ''],
    birthState: [this.p.birthState ?? ''],
    gender: [this.p.gender as Gender],
    isForeign: [this.p.isForeign],
    mobilePhone: [this.p.mobilePhone ?? '', zodValidator(mobilePhone)],
    email: [this.p.email ?? '', zodValidator(email)],
    street: [this.p.street ?? ''],
    extNumber: [this.p.extNumber ?? ''],
    neighborhood: [this.p.neighborhood ?? ''],
    postalCode: [this.p.postalCode ?? '', zodValidator(digits(5, 'El código postal tiene 5 dígitos.'))],
    municipality: [this.p.municipality ?? ''],
    state: [this.p.state ?? ''],
    source: [(this.p.source ?? null) as CandidateSource | null],
    notes: [this.p.notes ?? ''],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      const body: PersonRequest = {
        firstNames: v.firstNames.trim(),
        lastNamePaternal: v.lastNamePaternal.trim(),
        lastNameMaternal: text(v.lastNameMaternal),
        birthDate: text(v.birthDate),
        birthState: text(v.birthState),
        gender: v.gender,
        isForeign: v.isForeign,
        curp: text(v.curp),
        rfc: text(v.rfc),
        nss: text(v.nss),
        mobilePhone: text(v.mobilePhone),
        email: text(v.email),
        street: text(v.street),
        extNumber: text(v.extNumber),
        neighborhood: text(v.neighborhood),
        postalCode: text(v.postalCode),
        municipality: text(v.municipality),
        state: text(v.state),
        source: v.source,
        notes: text(v.notes),
      };
      await this.api.put(`/v1/people/${this.p.id}`, body);
      this.snack.open('Datos actualizados', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
