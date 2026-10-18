import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import { type DateKey, formatDateKey, todayKey } from '@platform/shared';
import { applyServerErrors, confirm, errorMessage, openSidePanel } from '@platform/ui';
import type { GeneratedPaymentFilesDto, PaymentBatchDto, PaymentBatchRowDto, PaymentFile, PaymentLineDto, PaymentState } from '../data/api-types';
import { CatalogsRepository } from '../data/repositories';
import { MANUAL_PAYMENT_METHODS, money, PAYMENT_STATE } from '../data/payroll-labels';
import { PaymentsRepository } from '../data/payroll-repositories';
import { AccountPanel, type AccountPanelData } from '../staff/account-panel';
import { PayingAccountPanel } from './paying-account-panel';

export interface PaymentPanelData {
  readonly periodId: string;
  readonly label: string;
}

const ACCOUNT_TYPE_SHORT = { own_account: 'Cuenta', debit_card: 'Tarjeta', clabe: 'CLABE' } as const;
const BATCH_STATUS = { generated: 'Generado', uploaded_to_bank: 'Subido al banco', completed: 'Completado', cancelled: 'Cancelado' } as const;

/**
 * «Pagar» (SPEC §5.5, PLAN D86): who can be paid and who not yet, the cuenta de cargo, «Generar archivos de pago» — the Banco
 * Azteca file and, for accounts the bank does not know yet, the cuentas frecuentes file —, then each file: download again, «Subido
 * al banco», a row the bank rejected, and the deposit date. The period's one total is the grid's; a file shows only what it pays.
 */
@Component({
  selector: 'app-payment-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatButton, MatIcon, MatProgressBar, MatFormField, MatLabel, MatHint, MatError, MatInput, MatSelect, MatOption],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Pagar · {{ data.label }}</h2>
    <mat-dialog-content>
      @if (panel.loading() && !panel.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (panel.value(); as p) {
        <section class="box">
          <div class="row">
            <div>
              <div class="muted">Cuenta de cargo</div>
              @if (p.payingAccount; as a) {
                <div>{{ a.holderName }} · Banco Azteca ·{{ a.last4 }}</div>
              } @else {
                <div class="negative">Falta la cuenta de cargo</div>
              }
            </div>
            <button matButton type="button" (click)="editAccount()">{{ p.payingAccount ? 'Cambiar' : 'Capturar' }}</button>
          </div>
        </section>

        <div class="totals">
          <div><span class="muted">Total neto a pagar</span><strong>{{ money(p.totalNet) }}</strong></div>
          @for (t of p.totals; track t.state) {
            <div>
              <span class="pill" [class]="state[t.state!].tone">{{ state[t.state!].label }}</span>
              <strong>{{ t.lines }}</strong>
              <span class="muted">{{ money(t.amount) }}</span>
            </div>
          }
        </div>

        @if (p.canGenerate) {
          <section class="box next">
            <h3>{{ p.status === 'paid' ? 'Archivo de pendientes' : 'Archivo del período' }}</h3>
            <p>{{ p.payable }} {{ p.payable === 1 ? 'pago' : 'pagos' }} por {{ money(p.payableAmount) }} en este archivo.</p>
            @if (+p.frequentAccounts > 0) {
              <p class="muted">{{ p.frequentAccounts }} {{ p.frequentAccounts === 1 ? 'cuenta nueva va' : 'cuentas nuevas van' }} también en el archivo de cuentas frecuentes: súbelo al banco primero.</p>
            }
            @if (p.status !== 'paid') {
              <p class="muted">Al generarlo el período queda Pagado: ya no cambia y las correcciones son retroactivos en el siguiente período.</p>
            }
            <div class="end">
              <button matButton="filled" type="button" (click)="generate(p.status === 'paid')" [disabled]="busy()">
                <mat-icon fontSet="material-symbols-outlined">account_balance</mat-icon>{{ p.status === 'paid' ? 'Generar archivo de pendientes' : 'Generar archivos de pago' }}
              </button>
            </div>
          </section>
        } @else if (p.cannotGenerate) {
          <p class="muted">{{ p.cannotGenerate }}</p>
        }

        @if (result(); as r) {
          <section class="box done" role="status">
            <h3>Archivos listos</h3>
            <a matButton="outlined" [href]="r.payments.url" download><mat-icon fontSet="material-symbols-outlined">download</mat-icon>{{ r.payments.fileName }}</a>
            @if (r.frequentAccounts; as f) {
              <a matButton="outlined" [href]="f.url" download><mat-icon fontSet="material-symbols-outlined">download</mat-icon>{{ f.fileName }}</a>
            }
            @for (s of r.skipped; track s) {
              <p class="negative">Fuera del archivo: {{ s }}</p>
            }
          </section>
        }

        @if (waiting().length) {
          <h3>Pendientes de pago</h3>
          @for (l of waiting(); track l.lineId) {
            <div class="line">
              <span>{{ l.fullName }}</span>
              <span class="num">{{ money(l.amount) }}</span>
              <span class="pill" [class]="state[l.state!].tone">{{ state[l.state!].label }}</span>
              @if (l.canPayManually) {
                <button matButton type="button" class="small" (click)="addAccount(l)">Agregar cuenta</button>
                <button matButton type="button" class="small" (click)="startManual(l)">Pagar fuera de archivo</button>
              }
            </div>
            @if (manualFor() === l.lineId) {
              <form class="reject" [formGroup]="manualForm" (ngSubmit)="recordManual(l)" novalidate>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Forma de pago</mat-label>
                  <mat-select formControlName="method">
                    @for (m of methods; track m.value) {
                      <mat-option [value]="m.value">{{ m.label }}</mat-option>
                    }
                  </mat-select>
                  <mat-error>{{ msg('method') }}</mat-error>
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Referencia (recibo, folio)</mat-label>
                  <input matInput formControlName="reference" autocomplete="off" maxlength="100" />
                  <mat-error>{{ msg('reference') }}</mat-error>
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>Fecha de pago</mat-label>
                  <input matInput type="date" formControlName="paidOn" [max]="today" />
                  <mat-error>{{ msg('paidOn') }}</mat-error>
                </mat-form-field>
                <button matButton="filled" type="submit" [disabled]="busy()">Registrar pago</button>
                <button matButton type="button" (click)="manualFor.set(null)">Cancelar</button>
              </form>
            }
          }
        }

        @if (manualPaid().length) {
          <h3>Pagados fuera de archivo</h3>
          @for (l of manualPaid(); track l.lineId) {
            <div class="line">
              <span>{{ l.fullName }}<span class="muted"> · {{ methodLabel(l) }}@if (l.manual?.reference) { · {{ l.manual!.reference }} } · {{ day(l.manual!.paidOn) }}</span></span>
              <span class="num">{{ money(l.amount) }}</span>
              <span class="pill positive">Pagado</span>
              <button matButton type="button" class="small" (click)="undoManual(l)" [disabled]="busy()">Deshacer</button>
            </div>
          }
        }

        @if (p.batches.length) {
          <h3>Archivos</h3>
          @for (b of p.batches; track b.id) {
            <section class="box batch">
              <div class="row">
                <div>
                  <strong>{{ b.kind === 'period_payment' ? 'Archivo del período' : 'Archivo de pendientes' }}</strong>
                  <div class="muted">{{ when(b.generatedAt) }} · {{ b.generatedBy }} · {{ b.rowCount }} {{ b.rowCount === 1 ? 'pago' : 'pagos' }} · en este archivo {{ money(b.totalAmount) }}</div>
                </div>
                <span class="pill" [class.positive]="b.status !== 'generated'">{{ batchStatus[b.status!] }}</span>
              </div>
              <div class="buttons">
                <button matButton type="button" (click)="download(b, 'payments')" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">download</mat-icon>Pagos</button>
                @if (+b.frequentAccountsCount > 0) {
                  <button matButton type="button" (click)="download(b, 'frequent_accounts')" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">download</mat-icon>Cuentas frecuentes · {{ b.frequentAccountsCount }}</button>
                }
                @if (b.status === 'generated') {
                  <button matButton="tonal" type="button" (click)="uploaded(b)" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">cloud_done</mat-icon>Marcar subido al banco</button>
                }
              </div>
              <details>
                <summary>Ver pagos</summary>
                @for (r of b.rows; track r.id) {
                  <div class="line">
                    <span>{{ r.fullName }}<span class="muted"> · {{ r.bank }} {{ accountType[r.accountType!] }} ·{{ r.last4 }}</span>@if (r.rejectionReason) { <span class="muted"> · {{ r.rejectionReason }}</span> }</span>
                    <span class="num">{{ money(r.amount) }}</span>
                    <span class="pill" [class]="state[rowState(r)].tone">{{ state[rowState(r)].label }}</span>
                    @if (r.status === 'paid' && b.status !== 'generated') {
                      <button matButton type="button" class="small" (click)="rejecting.set(r.id)">Rechazado</button>
                    }
                  </div>
                  @if (rejecting() === r.id) {
                    <div class="reject">
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>Motivo del rechazo</mat-label>
                        <input matInput #reason autocomplete="off" maxlength="200" />
                      </mat-form-field>
                      <button matButton="filled" type="button" (click)="reject(r, reason.value)" [disabled]="busy()">Guardar</button>
                      <button matButton type="button" (click)="rejecting.set(null)">Cancelar</button>
                    </div>
                  }
                }
              </details>
            </section>
          }
        }

        @if (p.status === 'paid') {
          <h3>Depósito</h3>
          <div class="reject">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>Fecha de depósito</mat-label>
              <input matInput type="date" #deposit [value]="p.depositDate ?? ''" />
              <mat-hint>Cuando el banco dispersó los pagos</mat-hint>
            </mat-form-field>
            <button matButton="tonal" type="button" (click)="setDeposit(deposit.value)" [disabled]="busy()">Guardar</button>
          </div>
        }
      }
      @if (panel.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }
    .box {
      padding: 10px 12px;
      margin-bottom: 12px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
      h3 {
        margin-top: 0;
      }
      p {
        margin: 4px 0;
      }
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .totals {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 12px;
      div {
        display: flex;
        flex-direction: column;
        gap: 2px;
        padding: 8px 12px;
        border-radius: 12px;
        border: 1px solid var(--mat-sys-outline-variant);
        min-width: 100px;
      }
    }
    .end {
      display: flex;
      justify-content: flex-end;
    }
    .done {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 6px;
      a {
        max-width: 100%;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }
    .buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin: 6px 0;
    }
    .line {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 8px;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      > span:first-child {
        flex: 1 1 180px;
        min-width: 0;
      }
    }
    .num {
      font-variant-numeric: tabular-nums;
    }
    .reject {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin: 6px 0;
      mat-form-field {
        flex: 1 1 200px;
      }
    }
    .negative {
      color: var(--mat-sys-error);
    }
    details summary {
      cursor: pointer;
      color: var(--mat-sys-primary);
      padding: 4px 0;
    }
  `,
})
export class PaymentPanel {
  readonly data = inject<PaymentPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<PaymentPanel>);
  private readonly repo = inject(PaymentsRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly state = PAYMENT_STATE;
  readonly batchStatus = BATCH_STATUS;
  readonly accountType = ACCOUNT_TYPE_SHORT;
  readonly money = money;
  readonly panel = this.repo.panel(() => this.data.periodId);
  readonly busy = signal(false);
  readonly result = signal<GeneratedPaymentFilesDto | null>(null);
  readonly rejecting = signal<string | null>(null);
  readonly manualFor = signal<string | null>(null);
  readonly methods = MANUAL_PAYMENT_METHODS;
  readonly today = todayKey();
  readonly manualForm = inject(FormBuilder).nonNullable.group({
    method: ['cash' as 'cash' | 'check' | 'other', Validators.required],
    reference: [''],
    paidOn: [todayKey() as string, Validators.required],
  });
  private readonly banks = inject(CatalogsRepository).catalogs();

  /** Lines owed money that no file has paid yet (and that the file that can be generated now leaves out). */
  readonly waiting = computed(() =>
    (this.panel.value()?.lines ?? []).filter(
      (l) => l.canPayManually || (!l.inNextFile && ['card_requested', 'missing_account', 'invalid_account', 'rejected'].includes(l.state!)),
    ),
  );
  readonly manualPaid = computed(() => (this.panel.value()?.lines ?? []).filter((l) => l.manual));

  msg(field: 'method' | 'reference' | 'paidOn'): string | null {
    return errorMessage(this.manualForm.get(field));
  }

  day(d: string): string {
    return formatDateKey(d as DateKey);
  }

  methodLabel(l: PaymentLineDto): string {
    return MANUAL_PAYMENT_METHODS.find((m) => m.value === l.manual?.method)?.label ?? '';
  }

  /** «Agregar cuenta»: the same panel as the employee's Cuentas de pago; once saved the line is Lista for the pending file. */
  addAccount(l: PaymentLineDto): void {
    openSidePanel<AccountPanel, AccountPanelData>(this.dialog, AccountPanel, {
      employeeId: l.employeeId,
      employeeName: l.fullName,
      banks: (this.banks.value()?.banks ?? []).filter((b) => b.isActive),
      account: null,
    });
  }

  startManual(l: PaymentLineDto): void {
    this.manualForm.reset({ method: 'cash', reference: '', paidOn: todayKey() });
    this.manualFor.set(l.lineId);
  }

  async recordManual(l: PaymentLineDto): Promise<void> {
    this.manualForm.markAllAsTouched();
    if (this.manualForm.invalid || this.busy()) return;
    const v = this.manualForm.getRawValue();
    this.busy.set(true);
    try {
      await this.repo.recordManual(l.lineId, { method: v.method, reference: v.reference.trim() || null, paidOn: v.paidOn as DateKey });
      this.manualFor.set(null);
      this.snack.open(`${l.fullName}: pago fuera de archivo registrado`, undefined, { duration: 4000 });
    } catch (e) {
      const left = e instanceof ApiError ? applyServerErrors(this.manualForm, e) : [];
      if (!(e instanceof ApiError) || left.length || !Object.keys(e.fieldErrors).length) {
        this.snack.open(e instanceof ApiError ? e.message : 'No se pudo completar.', 'Cerrar');
      }
    } finally {
      this.busy.set(false);
    }
  }

  async undoManual(l: PaymentLineDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: 'Deshacer el pago fuera de archivo',
      body: `${l.fullName} vuelve a quedar pendiente de pago por ${money(l.amount)}.`,
      confirm: 'Deshacer',
    });
    if (!ok) return;
    await this.run(async () => {
      await this.repo.undoManual(l.lineId);
      this.snack.open(`${l.fullName} vuelve a quedar pendiente`, undefined, { duration: 4000 });
    });
  }

  rowState(r: PaymentBatchRowDto): PaymentState {
    return r.status as PaymentState;
  }

  when(at: string | Date): string {
    return new Date(at).toLocaleString('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Monterrey' });
  }

  editAccount(): void {
    openSidePanel(this.dialog, PayingAccountPanel, null);
  }

  async generate(pending: boolean): Promise<void> {
    const p = this.panel.value();
    if (!p) return;
    const ok = await confirm(this.dialog, {
      title: pending ? 'Generar archivo de pendientes' : 'Generar archivos de pago',
      body: pending
        ? `${p.payable} pagos por ${money(p.payableAmount)}.`
        : `${p.payable} pagos por ${money(p.payableAmount)}. El período queda Pagado y su prenómina ya no cambia.`,
      confirm: 'Generar',
    });
    if (!ok) return;
    await this.run(async () => {
      const r = await this.repo.generate(this.data.periodId);
      this.result.set(r);
      this.snack.open(pending ? 'Archivo de pendientes generado' : 'Archivos de pago generados; el período está pagado', undefined, { duration: 5000 });
    });
  }

  async download(b: PaymentBatchDto, file: PaymentFile): Promise<void> {
    await this.run(async () => {
      const f = await this.repo.download(b.id, file);
      window.location.assign(f.url);
    });
  }

  async uploaded(b: PaymentBatchDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: 'Marcar subido al banco',
      body: 'Sus pagos quedan como Pagado y sus cuentas nuevas como registradas en cuentas frecuentes.',
      confirm: 'Marcar subido',
    });
    if (!ok) return;
    await this.run(async () => {
      await this.repo.uploaded(b.id);
      this.snack.open('Archivo marcado como subido al banco', undefined, { duration: 4000 });
    });
  }

  async reject(r: PaymentBatchRowDto, reason: string): Promise<void> {
    await this.run(async () => {
      await this.repo.reject(r.id, reason.trim());
      this.rejecting.set(null);
      this.snack.open(`${r.fullName} queda pendiente para el archivo de pendientes`, undefined, { duration: 4000 });
    });
  }

  async setDeposit(date: string): Promise<void> {
    await this.run(async () => {
      await this.repo.setDepositDate(this.data.periodId, date || null);
      this.snack.open('Fecha de depósito guardada', undefined, { duration: 3000 });
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await work();
    } catch (e) {
      this.snack.open(e instanceof ApiError ? (e.fieldErrors ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message) : e.message) : 'No se pudo completar.', 'Cerrar');
    } finally {
      this.busy.set(false);
    }
  }
}
