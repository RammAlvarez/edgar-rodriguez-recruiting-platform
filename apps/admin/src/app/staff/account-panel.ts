import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { DateKey } from '@platform/shared';
import type { BankAccountDto, BankAccountType, BankDto } from '../data/api-types';
import { ACCOUNT_TYPE, ACCOUNT_TYPES } from '../data/payroll-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { PanelState } from '../clients/panel-base';

export interface AccountPanelData {
  readonly employeeId: string;
  readonly employeeName: string;
  readonly banks: readonly BankDto[];
  /** null: «Agregar cuenta». An existing account changes titular, alias or vigencia only — the number never. */
  readonly account: BankAccountDto | null;
}

/**
 * A «cuenta de pago» (SPEC §5.8): banco, tipo, número (checked by type: 16 / 18 / 14 or 20 digits, card and CLABE check
 * digits), titular — the employee unless they ask to be paid through someone else — alias and vigencia. The number is
 * stored encrypted; afterwards only its last four digits are shown.
 */
@Component({
  selector: 'app-account-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatCheckbox, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ a ? 'Editar cuenta' : 'Agregar cuenta' }}</h2>
    <mat-dialog-content>
      <p class="muted">{{ data.employeeName }}@if (a) { · {{ a.bankName }} {{ type[a.accountType] }} •••• {{ a.last4 }} }</p>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="account-form" novalidate>
        @if (!a) {
          <div class="row">
            <mat-form-field>
              <mat-label>Banco</mat-label>
              <mat-select formControlName="bankId">
                @for (b of data.banks; track b.id) {
                  <mat-option [value]="b.id">{{ b.name }}</mat-option>
                }
              </mat-select>
              <mat-error>{{ s.msg('bankId') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Tipo de cuenta</mat-label>
              <mat-select formControlName="accountType">
                @for (t of types; track t.value) {
                  <mat-option [value]="t.value">{{ t.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <mat-form-field>
            <mat-label>Número</mat-label>
            <input matInput formControlName="accountNumber" inputmode="numeric" autocomplete="off" />
            <mat-hint>{{ hint() }}</mat-hint>
            <mat-error>{{ s.msg('accountNumber') }}</mat-error>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Titular</mat-label>
          <input matInput formControlName="holderName" autocomplete="off" />
          <mat-hint>Vacío = el empleado. Puede ser un familiar a quien se le paga.</mat-hint>
          <mat-error>{{ s.msg('holderName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Alias</mat-label>
          <input matInput formControlName="alias" autocomplete="off" placeholder="Cuenta de mi esposa" />
        </mat-form-field>
        @if (a) {
          <mat-form-field>
            <mat-label>Vigente hasta</mat-label>
            <input matInput type="date" formControlName="validTo" />
            <mat-hint>Al cerrar una cuenta deja de ser la principal y queda en el historial</mat-hint>
            <mat-error>{{ s.msg('validTo') }}</mat-error>
          </mat-form-field>
        } @else {
          <mat-checkbox formControlName="makePrimary">Hacerla la cuenta principal (recibe el pago desde el siguiente período)</mat-checkbox>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="account-form" [disabled]="s.busy()">Guardar cuenta</button>
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
export class AccountPanel {
  readonly data = inject<AccountPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<AccountPanel>);
  private readonly employees = inject(EmployeesRepository);
  private readonly snack = inject(MatSnackBar);
  readonly a = this.data.account;
  readonly types = ACCOUNT_TYPES;
  readonly type = ACCOUNT_TYPE;

  readonly form = inject(FormBuilder).nonNullable.group({
    bankId: [null as string | null, this.a ? [] : Validators.required],
    accountType: ['debit_card' as BankAccountType],
    accountNumber: ['', this.a ? [] : Validators.required],
    holderName: [this.a?.holderName ?? '', Validators.maxLength(40)],
    alias: [this.a?.alias ?? '', Validators.maxLength(60)],
    validTo: [(this.a?.validTo ?? '') as string],
    makePrimary: [true],
  });
  readonly s = new PanelState(this.form);
  private readonly accountType = toSignal(this.form.controls.accountType.valueChanges, { initialValue: 'debit_card' as BankAccountType });
  readonly hint = computed(() => ACCOUNT_TYPES.find((t) => t.value === this.accountType())?.hint ?? '');

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string) => (x.trim() === '' ? null : x.trim());
      if (this.a)
        await this.employees.updateAccount(this.data.employeeId, this.a.id, {
          holderName: text(v.holderName) ?? this.a.holderName,
          alias: text(v.alias),
          validTo: (text(v.validTo) as DateKey | null) ?? null,
        });
      else
        await this.employees.addAccount(this.data.employeeId, {
          bankId: v.bankId!,
          accountType: v.accountType,
          accountNumber: v.accountNumber,
          holderName: text(v.holderName),
          alias: text(v.alias),
          validFrom: null,
          makePrimary: v.makePrimary,
        });
      this.snack.open('Cuenta guardada', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
