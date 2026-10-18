import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOptgroup, MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { formatDateKey } from '@platform/shared';
import { confirm, openSidePanel, Session } from '@platform/ui';
import type { PayrollPeriodDto, PrepayrollExport, PrepayrollRowDto } from '../data/api-types';
import { money, PAYMENT_STATE, PERIOD_STATUS, periodLabel } from '../data/payroll-labels';
import { PrepayrollRepository } from '../data/payroll-repositories';
import { CardQueuePanel } from './card-queue-panel';
import { LinePanel, type LinePanelData } from './line-panel';
import { PaymentPanel, type PaymentPanelData } from './payment-panel';

const ACCOUNT_TYPE_SHORT = { own_account: 'Cuenta', debit_card: 'Tarjeta', clabe: 'CLABE' } as const;

/**
 * Nómina → Prenómina (SPEC §5.5): a period's grid in the client's Excel layout (SEM 38: B–W), the payment columns beside it,
 * the column footers (Σ of what is shown — for display, D76), a row's desglose, the editable cell (ajuste manual with a reason),
 * «Cerrar captura», the files (the Excel, the PDF summary and the receipts), «Pagar» (the bank files, S10) and «Tarjetas».
 */
@Component({
  selector: 'app-prepayroll-page',
  imports: [MatButton, MatFormField, MatLabel, MatIcon, MatProgressBar, MatSelect, MatOption, MatOptgroup, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page wide">
      <header class="page-header">
        <h1>Prenómina</h1>
        <div class="actions">
          <a matButton routerLink="/incidencias" [queryParams]="{ period: periodId() }"><mat-icon fontSet="material-symbols-outlined">edit_note</mat-icon>Incidencias</a>
          @if (canCards()) {
            <button matButton type="button" (click)="cards()">
              <mat-icon fontSet="material-symbols-outlined">credit_card</mat-icon>Tarjetas@if (queue.value()?.length) { · {{ queue.value()!.length }} por solicitar }
            </button>
          }
        </div>
      </header>

      <div class="toolbar">
        <mat-form-field subscriptSizing="dynamic" class="period">
          <mat-label>Período</mat-label>
          <mat-select [value]="periodId()" (valueChange)="periodId.set($event)">
            @for (g of groups(); track g.key) {
              <mat-optgroup [label]="g.label">
                @for (p of g.periods; track p.id) {
                  <mat-option [value]="p.id">{{ label(p) }} · {{ status[p.status!].label }}</mat-option>
                }
              </mat-optgroup>
            }
          </mat-select>
        </mat-form-field>
        @if (grid.value(); as g) {
          <span class="pill" [class]="status[g.period.status!].tone">{{ status[g.period.status!].label }}</span>
          <span class="spacer"></span>
          @if (g.period.status === 'capturing') {
            <button matButton="outlined" type="button" (click)="closeCapture()" [disabled]="busy()">
              <mat-icon fontSet="material-symbols-outlined">lock</mat-icon>Cerrar captura
            </button>
          }
          @if (canPay() && g.period.status !== 'capturing') {
            <button matButton="filled" type="button" (click)="pay()">
              <mat-icon fontSet="material-symbols-outlined">account_balance</mat-icon>{{ g.period.status === 'paid' ? 'Pagos' : 'Pagar' }}
            </button>
          }
          <button matButton="outlined" type="button" (click)="export('excel')" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">table_view</mat-icon>Excel</button>
          <button matButton="outlined" type="button" (click)="export('summary')" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">picture_as_pdf</mat-icon>Resumen PDF</button>
          <button matButton="outlined" type="button" (click)="export('receipts')" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">receipt_long</mat-icon>Recibos</button>
        }
      </div>

      @if (periods.value()?.length === 0) {
        <p class="list-empty">Aún no hay períodos. Se crean solos con el calendario de cada grupo de nómina.</p>
      }
      @if (grid.loading() && !grid.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (grid.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }

      @if (grid.value(); as g) {
        <div class="summary">
          <div><span class="muted">Personas</span><strong>{{ g.rows.length }}</strong></div>
          <div><span class="muted">Total neto a pagar</span><strong>{{ money(g.totalNet) }}</strong></div>
          <div><span class="muted">Sin cuenta</span><strong>{{ missing() }}</strong></div>
          <div><span class="muted">Con ajuste manual</span><strong>{{ adjusted() }}</strong></div>
        </div>
        @if (g.period.status === 'in_review') {
          <p class="banner"><mat-icon fontSet="material-symbols-outlined">rule</mat-icon>En revisión: los cambios de asistencia e incidencias se marcan como «durante revisión».</p>
        }
        @if (g.period.status === 'paid') {
          <p class="banner"><mat-icon fontSet="material-symbols-outlined">lock</mat-icon>Pagado: la prenómina ya no cambia; una corrección es un retroactivo en el siguiente período.</p>
        }
        <div class="grid-scroll" role="region" aria-label="Prenómina" tabindex="0">
          <table class="grid">
            <thead>
              <tr>
                <th>Fecha ingreso</th>
                <th class="sticky">Empleado</th>
                <th>Puesto</th>
                <th>Turno</th>
                <th class="num">Días laborados</th>
                <th class="num">Costo x hra</th>
                <th class="num">Festivo</th>
                <th class="num">SD IMSS</th>
                <th class="num">SD bolsillo</th>
                @for (c of g.columns; track c) {
                  <th class="num">{{ c }}</th>
                }
                <th class="num total">Total neto a pagar</th>
                <th>Comentarios</th>
                <th>Banco</th>
                <th>Cuenta</th>
                <th>Pago</th>
              </tr>
            </thead>
            <tbody>
              @for (r of g.rows; track r.lineId) {
                <tr>
                  <td>{{ date(r.hiredAt) }}</td>
                  <td class="sticky"><button type="button" class="link" (click)="open(r)">{{ r.fullName }}</button>@if (r.adjusted) { <span class="mark" title="Con ajuste manual">✎</span> }</td>
                  <td>{{ r.positionName }}</td>
                  <td>{{ r.shift }}</td>
                  <td class="num">{{ +r.daysWorked }}</td>
                  <td class="num">{{ +r.hourlyRate }}</td>
                  <td class="num">{{ +r.holidaysWorked || '' }}</td>
                  <td class="num">{{ money(r.dailySalaryImss) }}</td>
                  <td class="num">{{ money(r.dailySalaryPocket) }}</td>
                  @for (a of amounts(r); track $index) {
                    <td class="num cell" [class.editable]="g.editable" (click)="g.editable && open(r, g.columns[$index])">{{ money(a) }}</td>
                  }
                  <td class="num total">{{ money(r.totalNet) }}</td>
                  <td class="comments">{{ r.comments }}</td>
                  <td>{{ r.payment.bank }}</td>
                  <td>@if (r.payment.last4) { {{ accountType[r.payment.accountType!] }} ·{{ r.payment.last4 }} }@if (r.payment.thirdPartyHolder) { <div class="muted">A nombre de {{ r.payment.thirdPartyHolder }}</div> }</td>
                  <td><span class="pill" [class]="payment[r.payment.state!].tone">{{ payment[r.payment.state!].label }}</span></td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="14 + g.columns.length" class="list-empty">Nadie trabajó en este período.</td></tr>
              }
            </tbody>
            <tfoot>
              <tr>
                <td></td>
                <td class="sticky">Totales</td>
                <td colspan="7"></td>
                @for (f of footers(); track $index) {
                  <td class="num">{{ money(f) }}</td>
                }
                <td class="num total">{{ money(g.totalNet) }}</td>
                <td colspan="4"></td>
              </tr>
            </tfoot>
          </table>
        </div>
        <p class="muted note">
          Los totales de columna suman los importes mostrados y son solo informativos; lo que se paga es el TOTAL NETO A PAGAR de cada persona
          ({{ money(g.totalNet) }}). Toca un importe para ajustarlo con un motivo.
        </p>
      }
    </div>
  `,
  styles: `
    .wide {
      max-width: none;
    }
    .page-header .actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-bottom: 12px;
      .period {
        width: min(460px, 100%);
      }
      .spacer {
        flex: 1;
      }
    }
    .summary {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-bottom: 12px;
      div {
        display: flex;
        flex-direction: column;
        padding: 10px 16px;
        border-radius: 12px;
        background: var(--mat-sys-surface-container);
        min-width: 140px;
      }
      strong {
        font: var(--mat-sys-title-medium);
      }
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
    }
    .grid-scroll {
      overflow: auto;
      max-height: calc(100dvh - 300px);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .grid {
      border-collapse: separate;
      border-spacing: 0;
      font: var(--mat-sys-body-small);
      white-space: nowrap;
      th,
      td {
        padding: 6px 10px;
        border-bottom: 1px solid var(--mat-sys-outline-variant);
        background: var(--mat-sys-surface);
        text-align: left;
      }
      thead th {
        position: sticky;
        top: 0;
        z-index: 2;
        background: var(--mat-sys-surface-container);
        font-weight: 600;
        white-space: normal;
        min-width: 72px;
        vertical-align: bottom;
      }
      tfoot td {
        position: sticky;
        bottom: 0;
        z-index: 2;
        background: var(--mat-sys-surface-container);
        font-weight: 600;
      }
      .sticky {
        position: sticky;
        left: 0;
        z-index: 1;
        min-width: 220px;
      }
      thead .sticky,
      tfoot .sticky {
        z-index: 3;
      }
      .num {
        text-align: right;
        font-variant-numeric: tabular-nums;
      }
      .total {
        font-weight: 600;
      }
      .cell.editable {
        cursor: pointer;
        &:hover {
          background: var(--mat-sys-secondary-container);
        }
      }
      .comments {
        max-width: 220px;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }
    .link {
      all: unset;
      cursor: pointer;
      color: var(--mat-sys-primary);
      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
      }
    }
    .mark {
      margin-left: 4px;
      color: var(--mat-sys-tertiary);
    }
    .note {
      margin-top: 8px;
    }
  `,
})
export class PrepayrollPage {
  private readonly repo = inject(PrepayrollRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  readonly status = PERIOD_STATUS;
  readonly payment = PAYMENT_STATE;
  readonly accountType = ACCOUNT_TYPE_SHORT;
  readonly money = money;

  readonly periods = this.repo.periods();
  readonly periodId = signal<string | null>(this.params.get('period'));
  readonly grid = this.repo.grid(() => this.periodId());
  readonly queue = this.repo.cardQueue();
  private readonly me = inject(Session).me;
  readonly canPay = computed(() => this.me()?.grants.some((g) => g.module === 'payroll_payment_files') ?? false);
  readonly canCards = computed(() => this.me()?.grants.some((g) => g.module === 'payroll_card_requests') ?? false);
  readonly busy = signal(false);

  /** Periods by client · group, newest first. */
  readonly groups = computed(() => {
    const map = new Map<string, { key: string; label: string; periods: PayrollPeriodDto[] }>();
    for (const p of this.periods.value() ?? []) {
      const g = map.get(p.groupId) ?? { key: p.groupId, label: `${p.clientName} · ${p.groupName}`, periods: [] };
      g.periods.push(p);
      map.set(p.groupId, g);
    }
    return [...map.values()];
  });
  readonly footers = computed(() => (this.grid.value()?.footers ?? []) as string[]);
  readonly missing = computed(() => this.grid.value()?.rows.filter((r) => r.payment.state === 'missing_account').length ?? 0);
  readonly adjusted = computed(() => this.grid.value()?.rows.filter((r) => r.adjusted).length ?? 0);

  constructor() {
    // The period in the URL; by default the oldest one still open (what needs work), else the newest.
    effect(() => {
      const periods = this.periods.value();
      if (!periods?.length || periods.some((p) => p.id === untracked(this.periodId))) return;
      const open = periods.filter((p) => p.status !== 'paid' && Number(p.lines) > 0);
      this.periodId.set((open.at(-1) ?? periods[0]).id);
    });
    let first = true;
    effect(() => {
      const period = this.periodId();
      void this.router.navigate([], { queryParams: { period }, replaceUrl: true });
      if (first) {
        first = false;
        return;
      }
      untracked(() => this.grid.reload());
    });
  }

  label(p: PayrollPeriodDto): string {
    return periodLabel(p, p.weekly);
  }

  date(d: string): string {
    return formatDateKey(d as never);
  }

  amounts(r: PrepayrollRowDto): (string | null)[] {
    return r.amounts as (string | null)[];
  }

  open(r: PrepayrollRowDto, column: string | null = null): void {
    const g = this.grid.value();
    if (!g) return;
    openSidePanel<LinePanel, LinePanelData>(this.dialog, LinePanel, {
      lineId: r.lineId, columns: g.columns, column, editable: g.editable, current: column ? this.amounts(r)[g.columns.indexOf(column)] : null,
    });
  }

  pay(): void {
    const g = this.grid.value();
    if (!g) return;
    openSidePanel<PaymentPanel, PaymentPanelData>(this.dialog, PaymentPanel, { periodId: g.period.id, label: periodLabel(g.period, g.period.weekly) });
  }

  cards(): void {
    openSidePanel(this.dialog, CardQueuePanel, null);
  }

  async closeCapture(): Promise<void> {
    const g = this.grid.value();
    if (!g) return;
    const ok = await confirm(this.dialog, {
      title: 'Cerrar captura',
      body: 'Los días sin captura quedan como falta y el período pasa a revisión. Después solo se corrige con «Cambiar» y un motivo.',
      confirm: 'Cerrar captura',
    });
    if (!ok) return;
    await this.run(async () => {
      await this.repo.closeCapture(g.period.id);
      this.snack.open('Captura cerrada; el período está en revisión', undefined, { duration: 4000 });
    });
  }

  async export(kind: PrepayrollExport): Promise<void> {
    const g = this.grid.value();
    if (!g) return;
    await this.run(async () => {
      const file = await this.repo.export(g.period.id, kind);
      // The presigned GET answers «attachment», so the browser downloads it and stays on this page.
      window.location.assign(file.url);
      this.snack.open(`${file.fileName} generado`, undefined, { duration: 4000 });
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await work();
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo completar.', 'Cerrar');
    } finally {
      this.busy.set(false);
    }
  }
}
