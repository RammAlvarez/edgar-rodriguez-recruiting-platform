import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { MatTooltip } from '@angular/material/tooltip';
import { ApiError } from '@platform/api-client';
import { confirm, openSidePanel, Pager, type SortValue, UiPager } from '@platform/ui';
import type { DocumentTemplateDto } from '../data/api-types';
import { CatalogsRepository } from '../data/repositories';
import { CATALOGS, type CatalogDef, type Row } from './catalog-defs';
import { CatalogPanel, type CatalogPanelData } from './catalog-panel';
import { TemplatePanel, type TemplatePanelData } from './template-panel';

/**
 * Administración → Catálogos (SPEC §3.4). Rows are deactivated, never deleted, so past records keep their labels; only
 * a holiday on the wrong date is deleted. Statuses, stages, sources and priorities are fixed enums, not catalogs.
 */
@Component({
  selector: 'app-catalogs-page',
  imports: [MatSortModule, UiPager, MatTabGroup, MatTab, MatTableModule, MatButton, MatIconButton, MatIcon, MatProgressBar, MatTooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Catálogos</h1></header>
      @if (catalogs.loading() && !catalogs.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (catalogs.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (catalogs.value(); as c) {
        <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms" [selectedIndex]="tab()" (selectedIndexChange)="tab.set($event)">
          @for (def of defs; track def.key) {
            <mat-tab [label]="def.label">
              <div class="tab-actions">
                @if (def.key === 'holidays') {
                  <button matButton="outlined" type="button" (click)="statutory()">
                    <mat-icon fontSet="material-symbols-outlined">event_available</mat-icon>Agregar festivos de ley {{ year }}
                  </button>
                }
                <button matButton="filled" type="button" (click)="open(def, null)">
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Agregar
                </button>
              </div>
              <div class="table-card">
                <table mat-table matSort [matSortActive]="pagers[def.key].sort().active" [matSortDirection]="pagers[def.key].sort().direction" (matSortChange)="pagers[def.key].sortChange($event)" [dataSource]="pagers[def.key].slice(def.rows(c), rowValue)">
                  @for (col of def.columns; track col.key) {
                    <ng-container [matColumnDef]="col.key">
                      <th mat-header-cell [mat-sort-header]="col.key" *matHeaderCellDef [class.num]="col.numeric">{{ col.label }}</th>
                      <td mat-cell *matCellDef="let r" [class.num]="col.numeric" [class.mono]="col.key === 'code' || col.key === 'speiCode'">
                        {{ col.format ? col.format(r) : r[col.key] }}
                      </td>
                    </ng-container>
                  }
                  <ng-container matColumnDef="isActive">
                    <th mat-header-cell mat-sort-header="isActive" *matHeaderCellDef>Estatus</th>
                    <td mat-cell *matCellDef="let r"><span class="pill" [class.positive]="r.isActive">{{ r.isActive ? 'Activo' : 'Inactivo' }}</span></td>
                  </ng-container>
                  <ng-container matColumnDef="actions">
                    <th mat-header-cell *matHeaderCellDef></th>
                    <td mat-cell *matCellDef="let r" class="actions">
                      <button matIconButton type="button" aria-label="Eliminar" matTooltip="Eliminar" (click)="$event.stopPropagation(); deleteHoliday(r)">
                        <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                      </button>
                    </td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="columnsOf(def)"></tr>
                  <tr mat-row *matRowDef="let r; columns: columnsOf(def)" class="clickable" tabindex="0" (click)="open(def, r)" (keydown.enter)="open(def, r)"></tr>
                  <tr class="mat-mdc-row empty-row" *matNoDataRow>
                    <td [attr.colspan]="columnsOf(def).length">Sin registros. Los que agregues aparecerán aquí.</td>
                  </tr>
                </table>
              <ui-pager [pager]="templatePager" [total]="c.documentTemplates.length" />
                <ui-pager [pager]="pagers[def.key]" [total]="def.rows(c).length" />
              </div>
            </mat-tab>
          }
          <mat-tab label="Plantillas de documentos">
            <div class="tab-actions">
              <button matButton="filled" type="button" (click)="template(null)">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Agregar
              </button>
            </div>
            <div class="table-card">
              <table mat-table matSort [matSortActive]="templatePager.sort().active" [matSortDirection]="templatePager.sort().direction" (matSortChange)="templatePager.sortChange($event)" [dataSource]="templatePager.slice(c.documentTemplates, templateSortBy)">
                <ng-container matColumnDef="name">
                  <th mat-header-cell mat-sort-header="name" *matHeaderCellDef>Plantilla</th>
                  <td mat-cell *matCellDef="let t">{{ t.nameEs }}</td>
                </ng-container>
                <ng-container matColumnDef="scope">
                  <th mat-header-cell mat-sort-header="scope" *matHeaderCellDef>Para</th>
                  <td mat-cell *matCellDef="let t">{{ t.scope === 'candidate' ? 'Candidatos' : 'Empleados' }}</td>
                </ng-container>
                <ng-container matColumnDef="required">
                  <th mat-header-cell mat-sort-header="required" *matHeaderCellDef class="num">Obligatorios</th>
                  <td mat-cell *matCellDef="let t" class="num">{{ requiredCount(t) }}</td>
                </ng-container>
                <ng-container matColumnDef="optional">
                  <th mat-header-cell mat-sort-header="optional" *matHeaderCellDef class="num">Opcionales</th>
                  <td mat-cell *matCellDef="let t" class="num">{{ t.items.length - requiredCount(t) }}</td>
                </ng-container>
                <ng-container matColumnDef="default">
                  <th mat-header-cell mat-sort-header="default" *matHeaderCellDef>Por omisión</th>
                  <td mat-cell *matCellDef="let t">{{ t.isDefault ? 'Sí' : 'No' }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="templateColumns"></tr>
                <tr mat-row *matRowDef="let t; columns: templateColumns" class="clickable" tabindex="0" (click)="template(t)" (keydown.enter)="template(t)"></tr>
              </table>
            </div>
          </mat-tab>
        </mat-tab-group>
      }
    </div>
  `,
  styles: `
    .tab-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin: 16px 0 8px;
    }
    .actions {
      width: 48px;
      text-align: right;
    }
  `,
})
export class CatalogsPage {
  private readonly repo = inject(CatalogsRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly catalogs = this.repo.catalogs();
  readonly defs = CATALOGS;
  readonly pagers: Record<string, Pager> = Object.fromEntries(CATALOGS.map((d) => [d.key, new Pager()]));
  readonly rowValue = (key: string, row: Record<string, unknown>): SortValue => row[key] as SortValue;
  readonly templatePager = new Pager();
  readonly templateSortBy: Record<string, (t: DocumentTemplateDto) => SortValue> = {
    name: (t) => t.nameEs,
    scope: (t) => t.scope,
    required: (t) => this.requiredCount(t),
    optional: (t) => t.items.length - this.requiredCount(t),
    default: (t) => t.isDefault,
  };
  readonly tab = signal(0);
  readonly year = new Date().getFullYear();
  readonly templateColumns = ['name', 'scope', 'required', 'optional', 'default'];
  private readonly types = computed(() => this.catalogs.value()?.documentTypes ?? []);

  columnsOf(def: CatalogDef): string[] {
    return [...def.columns.map((c) => c.key), def.hasActive ? 'isActive' : 'actions'];
  }

  requiredCount(t: DocumentTemplateDto): number {
    return t.items.filter((i) => i.isRequired).length;
  }

  open(def: CatalogDef, row: Row | null): void {
    openSidePanel<CatalogPanel, CatalogPanelData>(this.dialog, CatalogPanel, { def, row });
  }

  template(template: DocumentTemplateDto | null): void {
    openSidePanel<TemplatePanel, TemplatePanelData>(this.dialog, TemplatePanel, {
      template,
      types: this.types().filter((t) => t.isActive || template?.items.some((i) => i.documentTypeId === t.id)),
    });
  }

  async statutory(): Promise<void> {
    await this.attempt(async () => {
      const r = await this.repo.addStatutoryHolidays(this.year);
      return r.added === 0 ? `Los festivos de ley ${this.year} ya estaban en el catálogo` : `${r.added} festivos de ley agregados`;
    });
  }

  async deleteHoliday(row: Row): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: '¿Eliminar día festivo?',
      body: `Se eliminará «${row['nameEs'] as string}». La asistencia de ese día dejará de contarse como festivo.`,
      confirm: 'Eliminar',
      destructive: true,
    });
    if (ok)
      await this.attempt(async () => {
        await this.repo.deleteHoliday(row.id);
        return 'Día festivo eliminado';
      });
  }

  private async attempt(work: () => Promise<string>): Promise<void> {
    try {
      this.snack.open(await work(), undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.', 'Cerrar', { duration: 8000 });
    }
  }
}
