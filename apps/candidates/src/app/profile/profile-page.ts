import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { curp, mobilePhone, rfcPerson } from '@platform/shared';
import { PanelState, zodValidator } from '@platform/ui/forms';
import { z } from 'zod';
import { CandidatePortal, type CandidateProfileDto } from '../data/portal';
import { PayAccount } from './pay-account';

const digits = (n: number, message: string) => z.string().trim().regex(new RegExp(`^\\d{${n}}$`), message);

interface Field {
  readonly name: string;
  readonly label: string;
  readonly type?: string;
  readonly hint?: string;
  readonly mono?: boolean;
  readonly autocomplete?: string;
  readonly inputmode?: string;
  /** Spans both columns of the form. */
  readonly full?: boolean;
}

/**
 * «Mi perfil» (SPEC §7): the candidate completes what the recruiter started — names, CURP, RFC, NSS, phone and address.
 * The email is how they sign in, so it is shown read-only. Mobile-first, one column.
 */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatHint, MatInput, MatButton, MatProgressBar, PayAccount],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      <header class="page-header"><h1>Mi perfil</h1></header>
      @if (profile.loading() && !profile.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (profile.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (profile.value(); as p) {
        <form class="panel-form" [formGroup]="form" (ngSubmit)="save(p)" novalidate>
          <h3 class="section-title">Identidad</h3>
          @for (f of identity; track f.name) {
            <mat-form-field [class.full]="f.full">
              <mat-label>{{ f.label }}</mat-label>
              <input matInput [formControlName]="f.name" [type]="f.type ?? 'text'" [class.mono]="f.mono"
                     [attr.autocomplete]="f.autocomplete ?? 'off'" [attr.inputmode]="f.inputmode ?? null" />
              @if (f.hint) {
                <mat-hint>{{ f.hint }}</mat-hint>
              }
              <mat-error>{{ s.msg(f.name) }}</mat-error>
            </mat-form-field>
          }
          <h3 class="section-title">Contacto</h3>
          <mat-form-field class="full">
            <mat-label>Correo</mat-label>
            <input matInput [value]="p.email ?? ''" readonly />
            <mat-hint>Es el correo con el que entras; para cambiarlo, pídeselo a tu reclutador</mat-hint>
          </mat-form-field>
          @for (f of contact; track f.name) {
            <mat-form-field [class.full]="f.full">
              <mat-label>{{ f.label }}</mat-label>
              <input matInput [formControlName]="f.name" [type]="f.type ?? 'text'"
                     [attr.autocomplete]="f.autocomplete ?? 'off'" [attr.inputmode]="f.inputmode ?? null" />
              @if (f.hint) {
                <mat-hint>{{ f.hint }}</mat-hint>
              }
              <mat-error>{{ s.msg(f.name) }}</mat-error>
            </mat-form-field>
          }
          @if (s.alert()) {
            <p class="form-alert" role="alert">{{ s.alert() }}</p>
          }
          <button matButton="filled" type="submit" class="save" [disabled]="s.busy()">Guardar</button>
        </form>
        <app-pay-account />
      }
    </div>
  `,
  styles: `
    .narrow {
      max-width: 720px;
    }
    // Two columns from tablet up, as the prototype's profile; phones keep one.
    @media (min-width: 600px) {
      .panel-form {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 10px 16px;
        .full,
        .section-title,
        .form-alert,
        .save {
          grid-column: 1 / -1;
        }
      }
    }
    .save {
      align-self: stretch;
      margin-top: 8px;
    }
  `,
})
export class ProfilePage {
  private readonly portal = inject(CandidatePortal);
  private readonly snack = inject(MatSnackBar);
  readonly profile = this.portal.profile();

  readonly identity: readonly Field[] = [
    { name: 'firstNames', label: 'Nombre(s)', full: true, autocomplete: 'given-name' },
    { name: 'lastNamePaternal', label: 'Apellido paterno', autocomplete: 'family-name' },
    { name: 'lastNameMaternal', label: 'Apellido materno' },
    { name: 'curp', label: 'CURP', mono: true, hint: 'Tu fecha de nacimiento se toma de la CURP' },
    { name: 'birthDate', label: 'Fecha de nacimiento', type: 'date', autocomplete: 'bday' },
    { name: 'birthState', label: 'Estado de nacimiento' },
    { name: 'rfc', label: 'RFC', mono: true, hint: '13 caracteres, con homoclave' },
    { name: 'nss', label: 'NSS (número de seguro social)', mono: true, hint: '11 dígitos', inputmode: 'numeric' },
  ];
  readonly contact: readonly Field[] = [
    { name: 'mobilePhone', label: 'Celular', type: 'tel', hint: '10 dígitos', autocomplete: 'tel-national', inputmode: 'numeric' },
    { name: 'street', label: 'Calle', full: true, autocomplete: 'address-line1' },
    { name: 'extNumber', label: 'Número' },
    { name: 'neighborhood', label: 'Colonia' },
    { name: 'postalCode', label: 'Código postal', autocomplete: 'postal-code', inputmode: 'numeric' },
    { name: 'municipality', label: 'Municipio', autocomplete: 'address-level2' },
    { name: 'state', label: 'Estado', autocomplete: 'address-level1' },
  ];

  readonly form = inject(FormBuilder).nonNullable.group({
    firstNames: ['', [Validators.required, Validators.maxLength(80)]],
    lastNamePaternal: ['', [Validators.required, Validators.maxLength(60)]],
    lastNameMaternal: [''],
    curp: ['', zodValidator(curp)],
    birthDate: [''],
    birthState: [''],
    rfc: ['', zodValidator(rfcPerson)],
    nss: ['', zodValidator(digits(11, 'El NSS tiene 11 dígitos.'))],
    mobilePhone: ['', zodValidator(mobilePhone)],
    street: [''],
    extNumber: [''],
    neighborhood: [''],
    postalCode: ['', zodValidator(digits(5, 'El código postal tiene 5 dígitos.'))],
    municipality: [''],
    state: [''],
  });
  readonly s = new PanelState(this.form);

  constructor() {
    // Fill the form from the server (again after «Actualizar») unless the candidate is typing.
    effect(() => {
      const p = this.profile.value();
      if (p && !this.form.dirty) this.fill(p);
    });
  }

  private fill(p: CandidateProfileDto): void {
    this.form.reset({
      firstNames: p.firstNames,
      lastNamePaternal: p.lastNamePaternal,
      lastNameMaternal: p.lastNameMaternal ?? '',
      curp: p.curp ?? '',
      birthDate: p.birthDate ?? '',
      birthState: p.birthState ?? '',
      rfc: p.rfc ?? '',
      nss: p.nss ?? '',
      mobilePhone: p.mobilePhone ?? '',
      street: p.street ?? '',
      extNumber: p.extNumber ?? '',
      neighborhood: p.neighborhood ?? '',
      postalCode: p.postalCode ?? '',
      municipality: p.municipality ?? '',
      state: p.state ?? '',
    });
  }

  save(current: CandidateProfileDto): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      const saved = await this.portal.saveProfile({
        firstNames: v.firstNames.trim(),
        lastNamePaternal: v.lastNamePaternal.trim(),
        lastNameMaternal: text(v.lastNameMaternal),
        curp: text(v.curp),
        birthDate: text(v.birthDate),
        birthState: text(v.birthState),
        // Keep a sex the recruiter set; when unknown the API takes it from the CURP.
        gender: current.gender === 'unspecified' ? null : current.gender,
        rfc: text(v.rfc),
        nss: text(v.nss),
        mobilePhone: text(v.mobilePhone),
        street: text(v.street),
        extNumber: text(v.extNumber),
        neighborhood: text(v.neighborhood),
        postalCode: text(v.postalCode),
        municipality: text(v.municipality),
        state: text(v.state),
      });
      this.fill(saved);
      this.snack.open('Tus datos se guardaron', undefined, { duration: 4000 });
    });
  }
}
