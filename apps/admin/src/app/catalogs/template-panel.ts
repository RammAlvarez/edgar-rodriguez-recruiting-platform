import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PanelState } from '../clients/panel-base';
import type { DocumentTemplateDto, DocumentTypeDto } from '../data/api-types';
import { CatalogsRepository } from '../data/repositories';

export interface TemplatePanelData {
  readonly template: DocumentTemplateDto | null;
  readonly types: readonly DocumentTypeDto[];
}

type Use = 'none' | 'optional' | 'required';

/** A document checklist (SPEC §3.2): for each document type, not asked / optional / required. */
@Component({
  selector: 'app-template-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatInput, MatSelect, MatOption, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.template ? 'Editar plantilla' : 'Nueva plantilla' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="template-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="nameEs" autocomplete="off" />
          <mat-error>{{ s.msg('nameEs') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Para</mat-label>
          <mat-select formControlName="scope">
            <mat-option value="candidate">Candidatos</mat-option>
            <mat-option value="employee">Empleados</mat-option>
          </mat-select>
          <mat-error>{{ s.msg('scope') }}</mat-error>
        </mat-form-field>
        <mat-slide-toggle formControlName="isDefault">Plantilla por omisión</mat-slide-toggle>
        <h3 class="section-title">Documentos</h3>
        <div [formGroup]="items" class="items">
          @for (t of data.types; track t.id) {
            <div class="item">
              <span>{{ t.nameEs }}</span>
              <mat-select [formControlName]="t.id" [attr.aria-label]="t.nameEs">
                <mat-option value="none">No se pide</mat-option>
                <mat-option value="optional">Opcional</mat-option>
                <mat-option value="required">Obligatorio</mat-option>
              </mat-select>
            </div>
          }
        </div>
        @if (s.msg('items'); as m) {
          <p class="form-alert" role="alert">{{ m }}</p>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="template-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .items {
      display: flex;
      flex-direction: column;
    }
    .item {
      display: grid;
      grid-template-columns: 1fr 140px;
      align-items: center;
      gap: 12px;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    mat-slide-toggle {
      margin: 4px 0 12px;
    }
  `,
})
export class TemplatePanel {
  readonly data = inject<TemplatePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<TemplatePanel>);
  private readonly repo = inject(CatalogsRepository);
  private readonly snack = inject(MatSnackBar);

  readonly items = new FormGroup(
    Object.fromEntries(
      this.data.types.map((t) => {
        const item = this.data.template?.items.find((i) => i.documentTypeId === t.id);
        return [t.id, new FormControl<Use>(item ? (item.isRequired ? 'required' : 'optional') : 'none', { nonNullable: true })];
      }),
    ),
  );
  readonly form = new FormGroup({
    nameEs: new FormControl(this.data.template?.nameEs ?? '', { nonNullable: true, validators: [Validators.required] }),
    scope: new FormControl(this.data.template?.scope ?? 'candidate', { nonNullable: true }),
    isDefault: new FormControl(this.data.template?.isDefault ?? false, { nonNullable: true }),
    items: this.items,
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      await this.repo.save('document-templates', this.data.template?.id ?? null, {
        nameEs: v.nameEs.trim(),
        scope: v.scope,
        isDefault: v.isDefault,
        items: Object.entries(v.items)
          .filter(([, use]) => use !== 'none')
          .map(([documentTypeId, use]) => ({ documentTypeId, isRequired: use === 'required' })),
      });
      this.snack.open('Plantilla guardada', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
