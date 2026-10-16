import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatTooltip } from '@angular/material/tooltip';
import type { components } from '@platform/shared';

type S = components['schemas'];
export type ChecklistItem = S['ChecklistItemDto'];
export type Checklist = S['DocumentChecklistDto'];
export type ChecklistDocument = S['PersonDocumentDto'];

const STATUS: Record<string, { label: string; tone: string }> = {
  pending: { label: 'Pendiente', tone: '' },
  delivered: { label: 'Entregado', tone: '' },
  validated: { label: 'Validado', tone: 'positive' },
  rejected: { label: 'Rechazado', tone: 'negative' },
  expired: { label: 'Vencido', tone: 'negative' },
};

/**
 * The document checklist of a person (SPEC §3.2): one row per document of the template with its status pill, the
 * completeness ring ("5 de 7 · 71 %"), upload, view and — for staff — Validar / Rechazar. Presentational: the parent
 * calls the API and passes the checklist back in; `uploading` is the document type being uploaded, with its progress.
 */
@Component({
  selector: 'ui-document-checklist',
  imports: [MatButton, MatIconButton, MatIcon, MatProgressBar, MatProgressSpinner, MatTooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let list = checklist();
    <div class="summary">
      <div class="ring">
        <mat-progress-spinner mode="determinate" [value]="percent()" diameter="56" strokeWidth="6" />
        <span class="pct">{{ percentText() }}</span>
      </div>
      <div>
        <div class="title">{{ doneCount() }} de {{ requiredCount() }} obligatorios entregados</div>
        <div class="muted">{{ list.templateName }}</div>
      </div>
    </div>
    <div class="rows">
      @for (item of list.items; track item.documentTypeId) {
        <div class="row">
          <mat-icon fontSet="material-symbols-outlined" class="muted">description</mat-icon>
          <div class="grow">
            <div class="name">
              {{ item.nameEs }}
              @if (!item.isRequired) {
                <span class="muted">· opcional</span>
              }
            </div>
            @if (item.current; as d) {
              <div class="muted">
                {{ d.originalFilename }}
                @if (d.expiresAt) { · vence {{ formatDate(d.expiresAt) }} }
                @if (d.status === 'rejected' && d.rejectionReason) { · {{ d.rejectionReason }} }
              </div>
            }
            @if (uploading()?.documentTypeId === item.documentTypeId) {
              <mat-progress-bar mode="determinate" [value]="(uploading()?.progress ?? 0) * 100" />
            }
          </div>
          <span class="pill" [class]="statusOf(item).tone">{{ statusOf(item).label }}</span>
          <div class="row-actions">
            @if (item.current; as d) {
              <button matIconButton type="button" aria-label="Ver documento" matTooltip="Ver documento" (click)="view.emit(d)">
                <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
              </button>
              @if (canReview() && d.status !== 'validated') {
                <button matButton type="button" (click)="validate.emit(d)">Validar</button>
              }
              @if (canReview() && d.status !== 'rejected') {
                <button matButton type="button" class="destructive" (click)="reject.emit(d)">Rechazar</button>
              }
            }
            <label class="upload">
              <input type="file" accept="application/pdf,image/jpeg,image/png" [disabled]="!!uploading()"
                     (change)="pick(item, $event)" />
              <span>{{ item.current ? replaceLabel() : uploadLabel() }}</span>
            </label>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .summary {
      display: flex;
      align-items: center;
      gap: 16px;
      margin: 16px 0;
      .title {
        font: var(--mat-sys-title-small);
      }
    }
    .ring {
      position: relative;
      width: 56px;
      height: 56px;
      .pct {
        position: absolute;
        inset: 0;
        display: grid;
        place-items: center;
        font: var(--mat-sys-label-medium);
      }
    }
    .rows {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      padding: 10px 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      &:last-child {
        border-bottom: 0;
      }
      .grow {
        flex: 1;
        min-width: 160px;
      }
      .name {
        font-weight: 500;
      }
    }
    .row-actions {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .upload {
      position: relative;
      display: inline-flex;
      align-items: center;
      height: 36px;
      padding: 0 12px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 18px;
      color: var(--mat-sys-primary);
      font: var(--mat-sys-label-large);
      cursor: pointer;
      &:focus-within {
        outline: 2px solid var(--mat-sys-primary);
      }
      input {
        position: absolute;
        inset: 0;
        opacity: 0;
        cursor: pointer;
      }
    }
  `,
})
export class DocumentChecklist {
  readonly checklist = input.required<Checklist>();
  readonly canReview = input(false);
  /** The candidate's wording ("Subir foto o PDF"); staff keep the short labels. */
  readonly uploadLabel = input('Subir');
  readonly replaceLabel = input('Reemplazar');
  readonly uploading = input<{ documentTypeId: string; progress: number } | null>(null);

  readonly upload = output<{ item: ChecklistItem; file: File }>();
  readonly view = output<ChecklistDocument>();
  readonly validate = output<ChecklistDocument>();
  readonly reject = output<ChecklistDocument>();

  statusOf(item: ChecklistItem) {
    return STATUS[item.current?.status ?? 'pending'] ?? STATUS['pending']!;
  }

  requiredCount(): number {
    return this.checklist().items.filter((i) => i.isRequired).length;
  }

  doneCount(): number {
    return this.checklist().items.filter((i) => i.isRequired && (i.current?.status === 'delivered' || i.current?.status === 'validated')).length;
  }

  /** The API's completeness (truncated to two decimals, PLAN D66); the ring only draws it. */
  percent(): number {
    return Number(this.checklist().completeness);
  }

  percentText(): string {
    return `${Math.trunc(this.percent())} %`;
  }

  formatDate(key: string): string {
    const [y, m, d] = key.split('-');
    return `${d}/${m}/${y}`;
  }

  pick(item: ChecklistItem, event: Event): void {
    const el = event.target as HTMLInputElement;
    const file = el.files?.[0];
    el.value = '';
    if (file) this.upload.emit({ item, file });
  }
}
