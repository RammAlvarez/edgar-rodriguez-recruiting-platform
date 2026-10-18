import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, type ValidatorFn, Validators } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { toSignal } from '@angular/core/rxjs-interop';
import { decimalString } from '@platform/shared';
import { zodValidator } from '@platform/ui';
import type { OpeningDetailDto } from '../data/api-types';
import { GENDER, PRIORITY, type Gender, type OpeningPriority } from '../data/recruiting-labels';
import { OpeningsRepository, RecruitersRepository } from '../data/recruiting-repositories';
import { CatalogsRepository, ClientsRepository } from '../data/repositories';
import { PanelState } from '../clients/panel-base';

export interface OpeningPanelData {
  /** Present when editing. */
  readonly opening?: OpeningDetailDto;
  /** «Nueva vacante» from a client's detail: Cliente preset and read-only (SPEC §4.5). */
  readonly clientId?: string;
}

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/** «Nueva vacante» / «Editar vacante» (SPEC §4.1, ARCHITECTURE §9 2a): every BD_VACANTES field, the status is never asked. */
/** A whole number from 1 (the API reads an integer: `1.5` would come back as an unreadable body). */
const headcount: ValidatorFn = (c) =>
  Number.isInteger(Number(c.value)) && Number(c.value) >= 1 ? null : { message: 'Indica cuántas vacantes (1 o más).' };

@Component({
  selector: 'app-opening-panel',
  imports: [
    ReactiveFormsModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatInput,
    MatSelect,
    MatOption,
    MatSlideToggle,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatButton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.opening ? 'Editar vacante' : 'Nueva vacante' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="opening-form" novalidate>
        <mat-form-field>
          <mat-label>Cliente</mat-label>
          <mat-select formControlName="clientId">
            @for (c of clients(); track c.id) {
              <mat-option [value]="c.id">{{ c.shortName }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('clientId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Zona (municipio)</mat-label>
          <mat-select formControlName="zoneId">
            <mat-option [value]="null">Sin zona</mat-option>
            @for (z of zones(); track z.id) {
              <mat-option [value]="z.id">{{ z.nameEs }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('zoneId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Puesto</mat-label>
          <input matInput formControlName="positionName" [matAutocomplete]="puestos" autocomplete="off" />
          <mat-autocomplete #puestos="matAutocomplete">
            @for (p of positionOptions(); track p) {
              <mat-option [value]="p">{{ p }}</mat-option>
            }
          </mat-autocomplete>
          <mat-error>{{ s.msg('positionName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Vacantes solicitadas</mat-label>
          <input matInput type="number" min="1" formControlName="headcountRequested" />
          <mat-error>{{ s.msg('headcountRequested') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Sueldo mensual</mat-label>
          <input matInput formControlName="monthlySalary" inputmode="decimal" autocomplete="off" />
          <mat-hint>En pesos, por ejemplo 9500.00</mat-hint>
          <mat-error>{{ s.msg('monthlySalary') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Prestaciones</mat-label>
          <mat-select formControlName="benefits" multiple>
            @for (b of benefits(); track b) {
              <mat-option [value]="b">{{ b }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('benefits') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Otras prestaciones</mat-label>
          <input matInput formControlName="otherBenefits" autocomplete="off" />
          <mat-hint>Separadas por coma</mat-hint>
          <mat-error>{{ s.msg('otherBenefits') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Horario</mat-label>
          <input matInput formControlName="schedule" autocomplete="off" />
          <mat-error>{{ s.msg('schedule') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Banco de pago</mat-label>
          <mat-select formControlName="paymentBankIds" multiple>
            @for (b of banks(); track b.id) {
              <mat-option [value]="b.id">{{ b.name }}</mat-option>
            }
          </mat-select>
          <mat-hint>Bancos con los que paga el cliente</mat-hint>
          <mat-error>{{ s.msg('paymentBankIds') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Nivel de estudios</mat-label>
          <mat-select formControlName="educationLevelId">
            <mat-option [value]="null">Sin requisito</mat-option>
            @for (e of educationLevels(); track e.id) {
              <mat-option [value]="e.id">{{ e.nameEs }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('educationLevelId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Años de experiencia</mat-label>
          <input matInput type="number" min="0" formControlName="yearsExperience" />
          <mat-error>{{ s.msg('yearsExperience') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Experiencia requerida</mat-label>
          <textarea matInput formControlName="requiredExperience" rows="2"></textarea>
          <mat-error>{{ s.msg('requiredExperience') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Sexo</mat-label>
          <mat-select formControlName="genderRequirement">
            @for (g of genders; track g.value) {
              <mat-option [value]="g.value">{{ g.label }}</mat-option>
            }
          </mat-select>
          <mat-hint>Solo interno; nunca se muestra al cliente</mat-hint>
          <mat-error>{{ s.msg('genderRequirement') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Edad</mat-label>
          <input matInput formControlName="ageRange" autocomplete="off" placeholder="25-45" />
          <mat-hint>Solo interno</mat-hint>
          <mat-error>{{ s.msg('ageRange') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Prioridad</mat-label>
          <mat-select formControlName="priority">
            @for (p of priorities; track p.value) {
              <mat-option [value]="p.value">{{ p.label }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('priority') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Reclutador líder</mat-label>
          <mat-select formControlName="leadRecruiterId">
            @if (!data.opening?.leadRecruiterId) {
              <mat-option [value]="null">Sin líder</mat-option>
            }
            @for (r of recruiters(); track r.id) {
              <mat-option [value]="r.id">{{ r.displayName }}</mat-option>
            }
          </mat-select>
          @if (data.opening?.leadRecruiterId) {
            <mat-hint>Para quitar al líder usa «Quitar» en Asignar reclutador</mat-hint>
          }
          <mat-error>{{ s.msg('leadRecruiterId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Documentos requeridos</mat-label>
          <mat-select formControlName="documentTemplateId">
            <mat-option [value]="null">Plantilla predeterminada</mat-option>
            @for (t of templates(); track t.id) {
              <mat-option [value]="t.id">{{ t.nameEs }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('documentTemplateId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Fecha de solicitud</mat-label>
          <input matInput type="date" formControlName="requestedAt" />
          <mat-error>{{ s.msg('requestedAt') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Observaciones</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
          <mat-error>{{ s.msg('notes') }}</mat-error>
        </mat-form-field>
        <mat-slide-toggle formControlName="visibleToClient">Visible en el portal del cliente</mat-slide-toggle>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="opening-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-slide-toggle {
      margin: 4px 0 12px;
    }
  `,
})
export class OpeningPanel {
  readonly data = inject<OpeningPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<OpeningPanel, OpeningDetailDto | undefined>);
  private readonly openings = inject(OpeningsRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly catalogs = inject(CatalogsRepository).catalogs();
  private readonly clientList = inject(ClientsRepository).options();
  private readonly recruiterList = inject(RecruitersRepository).list();

  readonly genders = (Object.keys(GENDER) as Gender[]).map((value) => ({ value, label: GENDER[value] }));
  readonly priorities = (Object.keys(PRIORITY) as OpeningPriority[]).map((value) => ({ value, label: PRIORITY[value] }));

  private readonly o = this.data.opening;
  private readonly catalogBenefits = computed(() => (this.catalogs.value()?.benefits ?? []).filter((b) => b.isActive).map((b) => b.nameEs));

  readonly form = inject(FormBuilder).group({
    clientId: [{ value: this.o?.clientId ?? this.data.clientId ?? null, disabled: !!this.data.clientId }, Validators.required],
    zoneId: [this.o?.zoneId ?? null],
    positionName: [this.o?.positionName ?? '', [Validators.required, Validators.maxLength(120)]],
    headcountRequested: [this.o ? Number(this.o.headcountRequested) : 1, [Validators.required, headcount]],
    monthlySalary: [this.o?.monthlySalary ?? '', zodValidator(decimalString())],
    benefits: [this.o?.benefits ?? ([] as string[])],
    otherBenefits: [''],
    schedule: [this.o?.schedule ?? '', Validators.maxLength(200)],
    paymentBankIds: [this.o?.paymentBankIds ?? ([] as string[])],
    educationLevelId: [this.o?.educationLevelId ?? null],
    yearsExperience: [this.o?.yearsExperience == null ? null : Number(this.o.yearsExperience), Validators.min(0)],
    requiredExperience: [this.o?.requiredExperience ?? ''],
    genderRequirement: [(this.o?.genderRequirement ?? 'unspecified') as Gender],
    ageRange: [this.o?.ageRange ?? ''],
    priority: [(this.o?.priority ?? 'medium') as OpeningPriority],
    leadRecruiterId: [this.o?.leadRecruiterId ?? null],
    documentTemplateId: [this.o?.documentTemplateId ?? null],
    requestedAt: [this.o?.requestedAt ?? today()],
    notes: [this.o?.notes ?? ''],
    visibleToClient: [this.o?.visibleToClient ?? true],
  });
  readonly s = new PanelState(this.form);

  readonly clients = computed(() => (this.clientList.value() ?? []).filter((c) => c.isActive || c.id === this.o?.clientId));
  readonly zones = computed(() => (this.catalogs.value()?.zones ?? []).filter((z) => z.isActive || z.id === this.o?.zoneId));
  readonly banks = computed(() => (this.catalogs.value()?.banks ?? []).filter((b) => b.isActive));
  readonly educationLevels = computed(() => (this.catalogs.value()?.educationLevels ?? []).filter((e) => e.isActive));
  readonly templates = computed(() => (this.catalogs.value()?.documentTemplates ?? []).filter((t) => t.scope === 'candidate'));
  readonly recruiters = computed(() => (this.recruiterList.value() ?? []).filter((r) => r.isActive));
  /** Catalog benefits plus any free-text ones the vacante already has, so editing keeps them checked. */
  readonly benefits = computed(() => [...new Set([...this.catalogBenefits(), ...(this.o?.benefits ?? [])])]);

  private readonly typedPosition = toSignal(this.form.controls.positionName.valueChanges, {
    initialValue: this.form.controls.positionName.value,
  });
  readonly positionOptions = computed(() => {
    const q = (this.typedPosition() ?? '').trim().toUpperCase();
    return (this.catalogs.value()?.positions ?? [])
      .filter((p) => p.isActive && (!q || p.nameEs.toUpperCase().includes(q)))
      .map((p) => p.nameEs)
      .slice(0, 20);
  });

  save(): Promise<void> {
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string | null | undefined) => (x == null || x.trim() === '' ? null : x.trim());
      const extra = (v.otherBenefits ?? '').split(',').map((b) => b.trim()).filter(Boolean);
      const saved = await this.openings.save(this.o?.id ?? null, {
        clientId: v.clientId!,
        zoneId: v.zoneId ?? null,
        positionName: v.positionName!.trim(),
        headcountRequested: Number(v.headcountRequested),
        monthlySalary: text(v.monthlySalary),
        benefits: [...(v.benefits ?? []), ...extra],
        schedule: text(v.schedule),
        paymentBankIds: v.paymentBankIds ?? [],
        educationLevelId: v.educationLevelId ?? null,
        yearsExperience: v.yearsExperience == null || `${v.yearsExperience}` === '' ? null : Number(v.yearsExperience),
        requiredExperience: text(v.requiredExperience),
        genderRequirement: v.genderRequirement!,
        ageRange: text(v.ageRange),
        priority: v.priority!,
        leadRecruiterId: v.leadRecruiterId ?? null,
        documentTemplateId: v.documentTemplateId ?? null,
        visibleToClient: v.visibleToClient ?? true,
        notes: text(v.notes),
        requestedAt: v.requestedAt || null,
      });
      this.snack.open(this.o ? 'Vacante actualizada' : 'Vacante creada', undefined, { duration: 4000 });
      this.ref.close(saved);
    });
  }
}
