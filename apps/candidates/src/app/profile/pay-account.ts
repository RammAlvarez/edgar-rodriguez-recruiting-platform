import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PanelState } from '@platform/ui/forms';
import { type BankAccountType, type BankOptionDto, CandidatePortal } from '../data/portal';

const TYPES: readonly { value: BankAccountType; label: string; hint: string }[] = [
  { value: 'debit_card', label: 'Tarjeta de débito', hint: 'Los 16 dígitos del frente de la tarjeta' },
  { value: 'clabe', label: 'CLABE', hint: '18 dígitos; viene en tu estado de cuenta o en la app de tu banco' },
  { value: 'own_account', label: 'Número de cuenta', hint: '14 o 20 dígitos' },
];

/**
 * «Cuenta para tu pago» (SPEC §7, PLAN D77): the account the candidate wants to be paid to once hired. The number is stored
 * encrypted and never shown again — only «•••• 1234». A new one replaces it as the principal.
 */
@Component({
  selector: 'app-pay-account',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <h2>Cuenta para tu pago</h2>
      @if (current(); as a) {
        <p>
          <strong>{{ a.bankName }}</strong> · {{ typeLabel(a.accountType) }} •••• {{ a.last4 }}<br />
          <span class="muted">A nombre de {{ a.holderName }}</span>
        </p>
        @if (!editing()) {
          <button matButton type="button" (click)="start()">Cambiar cuenta</button>
        }
      } @else if (!editing()) {
        <p class="muted">Si te contratan, aquí te pagamos. Si no tienes cuenta, te tramitamos una tarjeta.</p>
        <button matButton="filled" type="button" (click)="start()">Agregar mi cuenta</button>
      }
      @if (editing()) {
        <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <mat-form-field>
            <mat-label>Banco</mat-label>
            <mat-select formControlName="bankId">
              @for (b of banks(); track b.id) {
                <mat-option [value]="b.id">{{ b.name }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('bankId') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Tipo</mat-label>
            <mat-select formControlName="accountType">
              @for (t of types; track t.value) {
                <mat-option [value]="t.value">{{ t.label }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('accountType') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Número</mat-label>
            <input matInput formControlName="accountNumber" inputmode="numeric" autocomplete="off" />
            <mat-hint>{{ hint() }}</mat-hint>
            <mat-error>{{ s.msg('accountNumber') }}</mat-error>
          </mat-form-field>
          @if (s.alert()) {
            <p class="form-alert" role="alert">{{ s.alert() }}</p>
          }
          <div class="actions">
            <button matButton type="button" (click)="editing.set(false)">Cancelar</button>
            <button matButton="filled" type="submit" [disabled]="s.busy()">Guardar cuenta</button>
          </div>
        </form>
      }
    </section>
  `,
  styles: `
    .card {
      margin-top: 24px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      h2 {
        margin: 0 0 8px;
        font: var(--mat-sys-title-medium);
      }
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  `,
})
export class PayAccount {
  private readonly portal = inject(CandidatePortal);
  private readonly snack = inject(MatSnackBar);
  readonly accounts = this.portal.bankAccounts();
  readonly current = computed(() => (this.accounts.value() ?? []).find((a) => a.isPrimary));
  readonly banks = signal<BankOptionDto[]>([]);
  readonly editing = signal(false);
  readonly types = TYPES;

  readonly form = inject(FormBuilder).nonNullable.group({
    bankId: [null as string | null, Validators.required],
    accountType: ['debit_card' as BankAccountType],
    accountNumber: ['', Validators.required],
  });
  readonly s = new PanelState(this.form);
  private readonly type = toSignal(this.form.controls.accountType.valueChanges, { initialValue: 'debit_card' as BankAccountType });
  readonly hint = computed(() => TYPES.find((t) => t.value === this.type())?.hint ?? '');

  typeLabel(t: BankAccountType): string {
    return TYPES.find((x) => x.value === t)?.label ?? '';
  }

  async start(): Promise<void> {
    if (this.banks().length === 0) this.banks.set(await this.portal.banks());
    this.form.reset();
    this.editing.set(true);
  }

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      await this.portal.addBankAccount({
        bankId: v.bankId!, accountType: v.accountType, accountNumber: v.accountNumber, holderName: null, alias: null,
        validFrom: null, makePrimary: true,
      });
      this.snack.open('Guardamos tu cuenta', undefined, { duration: 4000 });
      this.editing.set(false);
    });
  }
}
