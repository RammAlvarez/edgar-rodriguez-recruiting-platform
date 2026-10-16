import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import { type ChecklistDocument, type ChecklistItem, DocumentChecklist } from '@platform/ui/document-checklist';
import { CandidatePortal } from '../data/portal';

/**
 * «Mis documentos» (SPEC §7): the checklist of the current application's template with each status — pendiente,
 * entregado, validado, rechazado (with the reason) — and «Subir foto o PDF»; on a phone the file picker offers the camera.
 */
@Component({
  selector: 'app-documents-page',
  imports: [DocumentChecklist, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      <header class="page-header"><h1>Mis documentos</h1></header>
      <p class="muted intro">Sube una foto clara o un PDF de cada documento (máximo 10 MB). Desde el celular puedes tomar la foto en ese momento.</p>
      @if (docs.loading() && !docs.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (docs.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (docs.value(); as list) {
        <ui-document-checklist [checklist]="list" [uploading]="uploading()" uploadLabel="Subir foto o PDF" replaceLabel="Subir otra vez"
                               (upload)="upload(list.personId, $event.item, $event.file)" (view)="view($event)" />
      }
    </div>
  `,
  styles: `
    .narrow {
      max-width: 720px;
    }
    .intro {
      margin: 0 0 8px;
    }
  `,
})
export class DocumentsPage {
  private readonly portal = inject(CandidatePortal);
  private readonly snack = inject(MatSnackBar);
  readonly docs = this.portal.documents();
  readonly uploading = signal<{ documentTypeId: string; progress: number } | null>(null);

  async upload(personId: string, item: ChecklistItem, file: File): Promise<void> {
    this.uploading.set({ documentTypeId: item.documentTypeId, progress: 0 });
    try {
      await this.portal.upload(personId, item, file, (progress) => this.uploading.set({ documentTypeId: item.documentTypeId, progress }));
      this.snack.open(`${item.nameEs}: entregado. Tu reclutador lo revisará.`, undefined, { duration: 5000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo subir el archivo. Revisa tu conexión e inténtalo de nuevo.',
        undefined, { duration: 6000 });
    } finally {
      this.uploading.set(null);
    }
  }

  async view(d: ChecklistDocument): Promise<void> {
    // Open the tab inside the click (popup blockers), then point it at the presigned URL.
    const tab = window.open('', '_blank');
    try {
      const { url } = await this.portal.url(d.id);
      if (tab) tab.location.href = url;
      else window.open(url, '_blank', 'noopener');
    } catch (e) {
      tab?.close();
      this.snack.open(e instanceof ApiError ? e.message : 'No se pudo abrir el documento.', undefined, { duration: 6000 });
    }
  }
}
