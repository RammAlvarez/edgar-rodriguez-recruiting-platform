import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PanelState } from '../clients/panel-base';
import { CatalogsRepository } from '../data/repositories';
import { type CatalogDef, type Row, toRequest } from './catalog-defs';

export interface CatalogPanelData {
  readonly def: CatalogDef;
  readonly row: Row | null;
}

/** The side panel of every simple catalog, built from its {@link CatalogDef}. */
@Component({
  selector: 'app-catalog-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatHint, MatInput, MatSelect, MatOption, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ (data.row ? 'Editar ' : 'Nuevo: ') + data.def.singular }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="catalog-form" novalidate>
        @for (f of data.def.fields; track f.key) {
          @switch (f.type) {
            @case ('toggle') {
              <mat-slide-toggle [formControlName]="f.key">{{ f.label }}</mat-slide-toggle>
            }
            @case ('select') {
              <mat-form-field>
                <mat-label>{{ f.label }}</mat-label>
                <mat-select [formControlName]="f.key">
                  @for (o of f.options; track o.value) {
                    <mat-option [value]="o.value">{{ o.label }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            @default {
              <mat-form-field>
                <mat-label>{{ f.label }}</mat-label>
                <input matInput [type]="f.type" [formControlName]="f.key" [readonly]="!!data.row && !!f.createOnly" autocomplete="off" />
                @if (f.hint) {
                  <mat-hint>{{ f.hint }}</mat-hint>
                }
                <mat-error>{{ s.msg(f.key) }}</mat-error>
              </mat-form-field>
            }
          }
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="catalog-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-slide-toggle {
      margin: 4px 0 12px;
    }
  `,
})
export class CatalogPanel {
  readonly data = inject<CatalogPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<CatalogPanel>);
  private readonly repo = inject(CatalogsRepository);
  private readonly snack = inject(MatSnackBar);

  readonly form = new FormGroup(
    Object.fromEntries(
      this.data.def.fields.map((f) => [
        f.key,
        new FormControl(this.data.row ? (this.data.row[f.key] ?? null) : (f.default ?? (f.type === 'toggle' ? false : '')), {
          validators: f.required ? [Validators.required] : [],
        }),
      ]),
    ),
  );
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      await this.repo.save(this.data.def.path, this.data.row?.id ?? null, toRequest(this.data.def, this.form.getRawValue()));
      this.snack.open('Catálogo actualizado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
