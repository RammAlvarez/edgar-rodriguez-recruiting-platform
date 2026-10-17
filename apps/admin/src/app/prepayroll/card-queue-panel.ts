import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatDialog, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { confirm } from '@platform/ui';
import type { GeneratedRegistrationDto, RegistrationBank, RegistrationBatchDto, RegistrationPreviewDto } from '../data/api-types';
import { REGISTRATION_BANK } from '../data/payroll-labels';
import { CardBatchesRepository, PrepayrollRepository } from '../data/payroll-repositories';

const BATCH_STATUS = { generated: 'Generado', uploaded_to_bank: 'Subido al banco', completed: 'Tarjetas recibidas', cancelled: 'Cancelado' } as const;
const REQUEST_STATUS = { queued: 'En espera', in_batch: 'En archivo', uploaded: 'Subido', received: 'Recibida', cancelled: 'Cancelada' } as const;

/**
 * «Tarjetas» (SPEC §5.5, §5.7, PLAN D85): the people waiting for a payroll card, by bank; «Revisar» checks them against the
 * bank's rules and «Generar alta» makes the Banorte (NOMINATIVA, sent to a sucursal) or Banregio file — without card numbers.
 * Then each file: «Subido al banco», and «Tarjeta recibida» with the number of the card that arrived, which becomes the person's
 * principal account.
 */
@Component({
  selector: 'app-card-queue-panel',
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatButton, MatCheckbox, MatFormField, MatLabel, MatHint, MatInput, MatIcon,
    MatProgressBar, MatTabGroup, MatTab, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Tarjetas</h2>
    <mat-dialog-content>
      <mat-tab-group animationDuration="0ms" mat-stretch-tabs="false">
        <mat-tab label="Por solicitar">
          <div class="banks">
            @for (b of banks; track b) {
              <button matButton [attr.aria-pressed]="bank() === b" [class.selected]="bank() === b" type="button" (click)="bank.set(b)">
                {{ bankLabel[b] }} · {{ countOf(b) }}
              </button>
            }
          </div>
          @if (queue.loading() && !queue.value()) {
            <mat-progress-bar mode="indeterminate" />
          }
          @for (r of queued(); track r.id) {
            <div class="row">
              <mat-checkbox [checked]="selected().has(r.id)" (change)="toggle(r.id)">
                <span>{{ r.fullName }}</span>
              </mat-checkbox>
              <span class="muted">{{ r.clientName }} · {{ since(r.requestedAt) }}</span>
            </div>
            @if (issuesOf(r.id); as issues) {
              @if (issues.length) {
                <ul class="issues">
                  @for (i of issues; track $index) {
                    <li>{{ i.field }}: {{ i.message }}</li>
                  }
                  <li><a [routerLink]="['/personal', r.employeeId]" (click)="ref.close()">Corregir en Personal</a></li>
                </ul>
              }
            }
          } @empty {
            @if (queue.value()) {
              <p class="list-empty">Nadie espera tarjeta de {{ bankLabel[bank()] }}.</p>
            }
          }
          @if (queued().length) {
            @if (bank() === 'banorte') {
              <mat-form-field class="full">
                <mat-label>Sucursal Banorte (5 dígitos)</mat-label>
                <input matInput inputmode="numeric" maxlength="5" autocomplete="off" [value]="branch()" (input)="branch.set($any($event.target).value)" />
                <mat-hint>Tarjetas NOMINATIVAS: el banco las envía a esta sucursal</mat-hint>
              </mat-form-field>
            }
            @if (preview(); as p) {
              @for (m of p.moot; track m) {
                <p class="muted">{{ m }} ya tiene cuenta: su solicitud se cancela.</p>
              }
              @for (f of p.fileIssues; track f) {
                <p class="negative">{{ f }}</p>
              }
            }
            <div class="end">
              <button matButton type="button" (click)="review()" [disabled]="busy()">Revisar</button>
              <button matButton="filled" type="button" (click)="generate()" [disabled]="busy() || !canGenerate()">
                <mat-icon fontSet="material-symbols-outlined">description</mat-icon>Generar alta
              </button>
            </div>
          }
          @if (generated(); as g) {
            <section class="box" role="status">
              <strong>Archivo listo</strong>
              <a matButton="outlined" [href]="g.file.url" download><mat-icon fontSet="material-symbols-outlined">download</mat-icon>{{ g.file.fileName }}</a>
              <p class="muted">Súbelo al portal del banco y márcalo como subido en Lotes.</p>
            </section>
          }
        </mat-tab>

        <mat-tab label="Lotes">
          @for (b of batches.value() ?? []; track b.id) {
            <section class="box">
              <div class="head">
                <div>
                  <strong>{{ bankLabel[b.bank!] }}</strong>@if (b.cardType) { <span class="muted"> · {{ b.cardType }} · sucursal {{ b.branchToSend }}</span> }
                  <div class="muted">{{ since(b.generatedAt) }} · {{ b.generatedBy }} · {{ b.rows.length }} {{ b.rows.length === 1 ? 'persona' : 'personas' }}</div>
                </div>
                <span class="pill" [class.positive]="b.status !== 'generated'">{{ batchStatus[b.status!] }}</span>
              </div>
              <div class="buttons">
                <button matButton type="button" (click)="download(b)" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">download</mat-icon>Archivo</button>
                @if (b.status === 'generated') {
                  <button matButton="tonal" type="button" (click)="uploaded(b)" [disabled]="busy()"><mat-icon fontSet="material-symbols-outlined">cloud_done</mat-icon>Marcar subido al banco</button>
                }
              </div>
              @for (r of b.rows; track r.requestId) {
                <div class="row">
                  <span>{{ r.fullName }}<span class="muted"> · núm. {{ r.employeeNumber }}</span></span>
                  <span class="pill" [class.positive]="r.status === 'received'">{{ requestStatus[r.status!] }}</span>
                </div>
                @if (r.status === 'uploaded') {
                  <div class="receive">
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>Número de la tarjeta recibida</mat-label>
                      <input matInput #card inputmode="numeric" autocomplete="off" maxlength="19" />
                    </mat-form-field>
                    <button matButton="filled" type="button" (click)="receive(r.requestId, r.fullName, card.value)" [disabled]="busy()">Tarjeta recibida</button>
                  </div>
                }
              }
            </section>
          } @empty {
            @if (batches.value()) {
              <p class="list-empty">Aún no hay archivos de alta.</p>
            }
          }
        </mat-tab>
      </mat-tab-group>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .banks {
      display: flex;
      gap: 8px;
      margin: 12px 0;
      .selected {
        background: var(--mat-sys-secondary-container);
      }
    }
    .row,
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 4px 8px;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .head {
      border-bottom: 0;
    }
    .issues {
      margin: 4px 0 8px 32px;
      padding: 0;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
    .full {
      width: 100%;
      margin-top: 12px;
    }
    .end {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 8px;
    }
    .box {
      padding: 10px 12px;
      margin: 12px 0;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
      display: flex;
      flex-direction: column;
      gap: 6px;
      a {
        align-self: flex-start;
        max-width: 100%;
      }
    }
    .buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .receive {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      padding: 6px 0;
      mat-form-field {
        flex: 1 1 200px;
      }
    }
    .negative {
      color: var(--mat-sys-error);
    }
  `,
})
export class CardQueuePanel {
  readonly ref = inject(MatDialogRef<CardQueuePanel>);
  private readonly repo = inject(CardBatchesRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly queue = inject(PrepayrollRepository).cardQueue();
  readonly batches = this.repo.list();
  readonly bankLabel = REGISTRATION_BANK;
  readonly batchStatus = BATCH_STATUS;
  readonly requestStatus = REQUEST_STATUS;
  readonly banks: readonly RegistrationBank[] = ['banorte', 'banregio'];

  readonly bank = signal<RegistrationBank>('banorte');
  readonly branch = signal('');
  readonly selected = signal(new Set<string>());
  readonly preview = signal<RegistrationPreviewDto | null>(null);
  readonly generated = signal<GeneratedRegistrationDto | null>(null);
  readonly busy = signal(false);

  readonly queued = computed(() => (this.queue.value() ?? []).filter((r) => r.bank === this.bank()));
  /** Every selected person passed the bank's checks, and Banorte has its sucursal. */
  readonly canGenerate = computed(() => {
    const p = this.preview();
    const chosen = this.selected();
    if (!p || p.bank !== this.bank() || chosen.size === 0) return false;
    if (this.bank() === 'banorte' && !/^\d{5}$/.test(this.branch())) return false;
    return p.rows.filter((r) => chosen.has(r.requestId)).every((r) => r.issues.length === 0);
  });

  constructor() {
    // A new bank (or a new queue) selects everyone waiting and forgets the last review.
    effect(() => {
      const ids = this.queued().map((r) => r.id);
      untracked(() => {
        this.selected.set(new Set(ids));
        this.preview.set(null);
      });
    });
  }

  countOf(bank: RegistrationBank): number {
    return (this.queue.value() ?? []).filter((r) => r.bank === bank).length;
  }

  issuesOf(requestId: string) {
    return this.preview()?.rows.find((r) => r.requestId === requestId)?.issues ?? null;
  }

  toggle(id: string): void {
    const next = new Set(this.selected());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selected.set(next);
  }

  since(at: string | Date): string {
    return new Date(at).toLocaleDateString('es-MX', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'America/Monterrey' });
  }

  async review(): Promise<void> {
    await this.run(async () => {
      this.preview.set(await this.repo.preview(this.bank(), this.bank() === 'banorte' ? this.branch() || null : null));
    });
  }

  async generate(): Promise<void> {
    await this.run(async () => {
      const g = await this.repo.generate({
        bank: this.bank(),
        requestIds: [...this.selected()],
        branchToSend: this.bank() === 'banorte' ? this.branch() : null,
      });
      this.generated.set(g);
      this.preview.set(null);
      const n = g.batch.rows.length;
      this.snack.open(`Alta ${this.bankLabel[this.bank()]} generada: ${n} ${n === 1 ? 'persona' : 'personas'}`, undefined, { duration: 4000 });
    });
  }

  async download(b: RegistrationBatchDto): Promise<void> {
    await this.run(async () => {
      window.location.assign((await this.repo.download(b.id)).url);
    });
  }

  async uploaded(b: RegistrationBatchDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: 'Marcar subido al banco',
      body: 'Cuando lleguen las tarjetas, captura el número de cada una aquí mismo.',
      confirm: 'Marcar subido',
    });
    if (!ok) return;
    await this.run(async () => {
      await this.repo.uploaded(b.id);
    });
  }

  async receive(requestId: string, name: string, card: string): Promise<void> {
    await this.run(async () => {
      const account = await this.repo.receive(requestId, card);
      this.snack.open(`${name}: la tarjeta ·${account.last4} es su cuenta principal`, undefined, { duration: 5000 });
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
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
