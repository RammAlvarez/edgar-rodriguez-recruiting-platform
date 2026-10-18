import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { zodValidator } from '@platform/ui';
import { z } from 'zod';
import type { SiteDto, ZoneDto } from '../data/api-types';
import { ClientsRepository } from '../data/repositories';
import { PanelState } from './panel-base';

export interface SitePanelData {
  readonly clientId: string;
  readonly site: SiteDto | null;
  readonly zones: readonly ZoneDto[];
}

/** «Nuevo sitio» (prototype): name, zona (municipio) and address. The client's Zona in the lists comes from its sites. */
@Component({
  selector: 'app-site-panel',
  imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, MatFormField, MatLabel, MatError, MatInput, MatSelect, MatOption, MatSlideToggle, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.site ? 'Editar sitio' : 'Nuevo sitio' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="site-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          <mat-error>{{ s.msg('name') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Zona</mat-label>
          <mat-select formControlName="zoneId">
            <mat-option [value]="null">—</mat-option>
            @for (z of zones; track z.id) {
              <mat-option [value]="z.id">{{ z.nameEs }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('zoneId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Calle</mat-label>
          <input matInput formControlName="street" autocomplete="off" />
          <mat-error>{{ s.msg('street') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Número</mat-label>
          <input matInput formControlName="extNumber" autocomplete="off" />
          <mat-error>{{ s.msg('extNumber') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Colonia</mat-label>
          <input matInput formControlName="neighborhood" autocomplete="off" />
          <mat-error>{{ s.msg('neighborhood') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Código postal</mat-label>
          <input matInput formControlName="postalCode" inputmode="numeric" autocomplete="off" />
          <mat-error>{{ s.msg('postalCode') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Municipio</mat-label>
          <input matInput formControlName="municipality" autocomplete="off" />
          <mat-error>{{ s.msg('municipality') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Estado</mat-label>
          <input matInput formControlName="state" autocomplete="off" />
          <mat-error>{{ s.msg('state') }}</mat-error>
        </mat-form-field>
        @if (data.site) {
          <mat-slide-toggle formControlName="isActive">Activo</mat-slide-toggle>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="site-form" [disabled]="s.busy()">Guardar sitio</button>
    </mat-dialog-actions>
  `,
})
export class SitePanel {
  readonly data = inject<SitePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<SitePanel>);
  private readonly clients = inject(ClientsRepository);
  private readonly snack = inject(MatSnackBar);
  readonly zones = this.data.zones.filter((z) => z.isActive || z.id === this.data.site?.zoneId);

  readonly form = inject(FormBuilder).group({
    name: [this.data.site?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    zoneId: [this.data.site?.zoneId ?? (null as string | null)],
    street: [this.data.site?.street ?? ''],
    extNumber: [this.data.site?.extNumber ?? ''],
    neighborhood: [this.data.site?.neighborhood ?? ''],
    postalCode: [this.data.site?.postalCode ?? '', zodValidator(z.string().regex(/^\d{5}$/, 'El código postal tiene 5 dígitos.'))],
    municipality: [this.data.site?.municipality ?? ''],
    state: [this.data.site?.state ?? 'Nuevo León'],
    isActive: [this.data.site?.isActive ?? true],
  });
  readonly s = new PanelState(this.form);

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string | null) => (!x || x.trim() === '' ? null : x.trim());
      await this.clients.saveSite(this.data.clientId, this.data.site?.id ?? null, {
        name: v.name!.trim(),
        zoneId: v.zoneId ?? null,
        street: text(v.street),
        extNumber: text(v.extNumber),
        neighborhood: text(v.neighborhood),
        postalCode: text(v.postalCode),
        municipality: text(v.municipality),
        state: text(v.state),
        isActive: v.isActive ?? true,
      });
      this.snack.open('Sitio guardado', undefined, { duration: 4000 });
      this.ref.close(true);
    });
  }
}
