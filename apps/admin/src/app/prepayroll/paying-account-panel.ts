import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PanelState } from '@platform/ui';
import { PaymentsRepository } from '../data/payroll-repositories';

/**
 * «Cuenta de cargo» (DATA_MODEL §6.1): the Banco Azteca account every payment file charges — MAXIMA REPARACION Y LIMPIEZA SA DE
 * CV. The number is shown only by its last four digits; typing a new one replaces it, leaving it empty keeps it.
 */
@Component({
  selector: 'app-paying-account-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Cuenta de cargo</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="paying-account-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          <mat-error>{{ s.msg('name') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Número de cuenta de cargo (Banco Azteca)</mat-label>
          <input matInput formControlName="chargeAccountNumber" inputmode="numeric" autocomplete="off" />
          @if (account.value(); as a) {
            <mat-hint>Guardada ·{{ a.last4 }}. Escribe otra solo para cambiarla.</mat-hint>
          } @else {
            <mat-hint>14, 16, 18 o 20 dígitos</mat-hint>
          }
          <mat-error>{{ s.msg('chargeAccountNumber') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Titular</mat-label>
          <input matInput formControlName="holderName" autocomplete="off" maxlength="40" />
          <mat-error>{{ s.msg('holderName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>RFC del titular</mat-label>
          <input matInput formControlName="holderRfc" autocomplete="off" />
          <mat-error>{{ s.msg('holderRfc') }}</mat-error>
        </mat-form-field>
        <div class="row">
          <mat-form-field>
            <mat-label>Leyenda del cargo</mat-label>
            <input matInput formControlName="chargeLegend" autocomplete="off" maxlength="30" />
            <mat-error>{{ s.msg('chargeLegend') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Leyenda del abono</mat-label>
            <input matInput formControlName="creditLegend" autocomplete="off" maxlength="30" />
            <mat-error>{{ s.msg('creditLegend') }}</mat-error>
          </mat-form-field>
        </div>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="paying-account-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      mat-form-field {
        flex: 1 1 160px;
      }
    }
  `,
})
export class PayingAccountPanel {
  readonly ref = inject(MatDialogRef<PayingAccountPanel>);
  private readonly repo = inject(PaymentsRepository);
  private readonly snack = inject(MatSnackBar);
  readonly account = this.repo.payingAccount();

  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['Cuenta Azteca — MAXIMA', [Validators.required, Validators.maxLength(80)]],
    chargeAccountNumber: [''],
    holderName: ['MAXIMA REPARACION Y LIMPIEZA SA DE CV', [Validators.required, Validators.maxLength(40)]],
    holderRfc: ['', Validators.required],
    chargeLegend: ['PAGO', Validators.maxLength(30)],
    creditLegend: ['PAGO', Validators.maxLength(30)],
  });
  readonly s = new PanelState(this.form);

  constructor() {
    effect(() => {
      const a = this.account.value();
      if (a)
        this.form.patchValue({ name: a.name, holderName: a.holderName, holderRfc: a.holderRfc, chargeLegend: a.chargeLegend, creditLegend: a.creditLegend });
    });
  }

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const saved = await this.repo.setPayingAccount({
        name: v.name.trim(),
        chargeAccountNumber: v.chargeAccountNumber.replace(/\D/g, '') || null,
        holderName: v.holderName.trim(),
        holderRfc: v.holderRfc.trim(),
        chargeLegend: v.chargeLegend.trim() || null,
        creditLegend: v.creditLegend.trim() || null,
      });
      this.snack.open(`Cuenta de cargo ·${saved.last4} guardada`, undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
