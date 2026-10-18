import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocomplete, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel, MatPrefix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { API_CLIENT, zodValidator } from '@platform/ui';
import { curp, decimalString, email, mobilePhone, rfcPerson } from '@platform/shared';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import type { CandidateDetailDto, PersonSummaryDto } from '../data/api-types';
import { SOURCES, type CandidateSource } from '../data/recruiting-labels';
import { CandidatesRepository, OpeningsRepository, PeopleRepository } from '../data/recruiting-repositories';
import { PanelState } from '../clients/panel-base';

export interface CandidatePanelData {
  /** «Nuevo candidato» from a vacante: the Vacante is preset and read-only. */
  readonly openingId?: string;
  /** Recruiter workspace / cover mode: the API limits the vacantes to hers; nothing changes in the form. */
  readonly workspace?: boolean;
}

interface Duplicate {
  readonly id: string;
  readonly fullName: string;
}

/**
 * «Nuevo candidato» (SPEC §4.2, ARCHITECTURE §9 2b): find the person in the registry (nombre, CURP, celular) or capture a
 * new one, then the vacante as "Cliente – Puesto" (open / in-process only) with its one-line summary, fuente and sueldo.
 */
@Component({
  selector: 'app-candidate-panel',
  imports: [
    ReactiveFormsModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatPrefix,
    MatInput,
    MatSelect,
    MatOption,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatButtonToggleGroup,
    MatButtonToggle,
    MatButton,
    MatIcon,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Nuevo candidato</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="candidate-form" novalidate>
        <h3 class="section-title">Persona</h3>
        <mat-button-toggle-group [value]="mode()" (change)="mode.set($event.value)" aria-label="Persona" hideSingleSelectionIndicator>
          <mat-button-toggle value="existing">Buscar en el registro</mat-button-toggle>
          <mat-button-toggle value="new">Crear nueva persona</mat-button-toggle>
        </mat-button-toggle-group>
        @if (mode() === 'existing') {
          <mat-form-field>
            <mat-label>Nombre, CURP o celular</mat-label>
            <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
            <input matInput [formControl]="search" [matAutocomplete]="people" autocomplete="off" />
            <mat-autocomplete #people="matAutocomplete" [displayWith]="personName" (optionSelected)="pick($event.option.value)">
              @for (p of matches(); track p.id) {
                <mat-option [value]="p">
                  {{ p.fullName }} <span class="muted">· {{ p.curp ?? p.mobilePhone ?? 'sin CURP' }}</span>
                </mat-option>
              }
            </mat-autocomplete>
            @if (person()) {
              <mat-hint>Seleccionada: {{ person()!.fullName }}</mat-hint>
            } @else {
              <mat-hint>Escribe al menos 3 letras</mat-hint>
            }
          </mat-form-field>
        } @else {
          <div formGroupName="person" class="person">
            <mat-form-field>
              <mat-label>Nombre(s)</mat-label>
              <input matInput formControlName="firstNames" autocomplete="off" />
              <mat-error>{{ s.msg('person.firstNames') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Apellido paterno</mat-label>
              <input matInput formControlName="lastNamePaternal" autocomplete="off" />
              <mat-error>{{ s.msg('person.lastNamePaternal') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Apellido materno</mat-label>
              <input matInput formControlName="lastNameMaternal" autocomplete="off" />
              <mat-error>{{ s.msg('person.lastNameMaternal') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>CURP</mat-label>
              <input matInput formControlName="curp" class="mono" autocomplete="off" (blur)="checkDuplicate()" />
              <mat-hint>La fecha de nacimiento y el sexo se toman de la CURP</mat-hint>
              <mat-error>{{ s.msg('person.curp') }}</mat-error>
            </mat-form-field>
            @if (duplicate(); as d) {
              <p class="form-alert duplicate">
                Ya existe una persona con esa CURP: <strong>{{ d.fullName }}</strong>.
                <button matButton type="button" (click)="useDuplicate(d)">Usar esta persona</button>
              </p>
            }
            <mat-form-field>
              <mat-label>RFC</mat-label>
              <input matInput formControlName="rfc" class="mono" autocomplete="off" />
              <mat-error>{{ s.msg('person.rfc') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Celular</mat-label>
              <input matInput type="tel" formControlName="mobilePhone" autocomplete="off" />
              <mat-hint>10 dígitos</mat-hint>
              <mat-error>{{ s.msg('person.mobilePhone') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Correo</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="off" />
              <mat-error>{{ s.msg('person.email') }}</mat-error>
            </mat-form-field>
          </div>
        }

        <h3 class="section-title">Postulación</h3>
        <mat-form-field>
          <mat-label>Vacante</mat-label>
          <mat-select formControlName="jobOpeningId">
            @for (o of openings(); track o.id) {
              <mat-option [value]="o.id">{{ o.displayName }}</mat-option>
            }
          </mat-select>
          @if (selectedOpening(); as o) {
            <mat-hint>Solicitadas {{ o.headcountRequested }} · Contratadas {{ o.hired }} · Reclutador {{ o.leadRecruiterName ?? 'sin líder' }}</mat-hint>
          } @else if (openings().length === 0) {
            <mat-hint>No hay vacantes abiertas o en proceso.</mat-hint>
          }
          <mat-error>{{ s.msg('jobOpeningId') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Fuente</mat-label>
          <mat-select formControlName="source">
            @for (src of sources; track src.value) {
              <mat-option [value]="src.value">{{ src.label }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ s.msg('source') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Sueldo ofrecido</mat-label>
          <input matInput formControlName="offeredSalary" inputmode="decimal" autocomplete="off" />
          <mat-hint>Mensual, en pesos</mat-hint>
          <mat-error>{{ s.msg('offeredSalary') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Comentarios</mat-label>
          <textarea matInput formControlName="comments" rows="3"></textarea>
          <mat-error>{{ s.msg('comments') }}</mat-error>
        </mat-form-field>
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="candidate-form" [disabled]="s.busy()">Guardar</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-button-toggle-group {
      margin-bottom: 12px;
    }
    .person {
      display: flex;
      flex-direction: column;
    }
    .duplicate {
      margin: 0 0 8px;
    }
  `,
})
export class CandidatePanel {
  readonly data = inject<CandidatePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<CandidatePanel, CandidateDetailDto | undefined>);
  private readonly candidates = inject(CandidatesRepository);
  private readonly peopleRepo = inject(PeopleRepository);
  private readonly api = inject(API_CLIENT);
  private readonly snack = inject(MatSnackBar);
  private readonly options = inject(OpeningsRepository).options();

  readonly sources = SOURCES;
  readonly mode = signal<'existing' | 'new'>('existing');
  readonly person = signal<PersonSummaryDto | null>(null);
  readonly duplicate = signal<Duplicate | null>(null);

  private readonly fb = inject(FormBuilder);
  readonly search = this.fb.nonNullable.control('');
  readonly form = this.fb.group({
    person: this.fb.nonNullable.group({
      firstNames: ['', [Validators.required, Validators.maxLength(80)]],
      lastNamePaternal: ['', [Validators.required, Validators.maxLength(60)]],
      lastNameMaternal: ['', Validators.maxLength(60)],
      curp: ['', zodValidator(curp)],
      rfc: ['', zodValidator(rfcPerson)],
      mobilePhone: ['', zodValidator(mobilePhone)],
      email: ['', zodValidator(email)],
    }),
    jobOpeningId: [{ value: this.data.openingId ?? (null as string | null), disabled: !!this.data.openingId }, Validators.required],
    source: [null as CandidateSource | null, Validators.required],
    offeredSalary: ['', zodValidator(decimalString())],
    comments: ['', Validators.maxLength(2000)],
  });
  readonly s = new PanelState(this.form);

  private readonly selectedId = toSignal(this.form.controls.jobOpeningId.valueChanges, { initialValue: this.data.openingId ?? null });
  readonly openings = computed(() => this.options.value() ?? []);
  readonly selectedOpening = computed(() => this.openings().find((o) => o.id === (this.form.controls.jobOpeningId.value ?? this.selectedId())));

  readonly matches = toSignal(
    this.search.valueChanges.pipe(
      filter((v): v is string => typeof v === 'string'),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((v) => (v.trim().length >= 3 ? this.peopleRepo.search(v.trim()) : Promise.resolve([] as PersonSummaryDto[]))),
    ),
    { initialValue: [] as PersonSummaryDto[] },
  );

  readonly personName = (p: PersonSummaryDto | string | null) => (typeof p === 'string' ? p : (p?.fullName ?? ''));

  pick(p: PersonSummaryDto): void {
    this.person.set(p);
  }

  async checkDuplicate(): Promise<void> {
    const value = this.form.controls.person.controls.curp.value.trim().toUpperCase();
    this.duplicate.set(null);
    if (value.length !== 18) return;
    const found = await this.api.get<{ id: string; fullName: string; isDeleted: boolean }[]>('/v1/people/duplicates', { curp: value });
    const live = found.find((d) => !d.isDeleted);
    if (live) this.duplicate.set({ id: live.id, fullName: live.fullName });
  }

  useDuplicate(d: Duplicate): void {
    this.person.set({ id: d.id, fullName: d.fullName, curp: null, rfc: null, mobilePhone: null, email: null });
    this.search.setValue(d.fullName, { emitEvent: false });
    this.mode.set('existing');
    this.duplicate.set(null);
  }

  save(): Promise<void> {
    // The person block is validated only when capturing a new person.
    const personGroup = this.form.controls.person;
    if (this.mode() === 'existing') personGroup.disable({ emitEvent: false });
    else personGroup.enable({ emitEvent: false });
    if (this.mode() === 'existing' && !this.person()) {
      this.s.alert.set('Busca y elige a la persona, o captura una nueva.');
      return Promise.resolve();
    }
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string | null | undefined) => (x == null || x.trim() === '' ? null : x.trim());
      const p = v.person;
      const created = await this.candidates.create({
        personId: this.mode() === 'existing' ? this.person()!.id : null,
        person:
          this.mode() === 'new'
            ? {
                firstNames: p.firstNames.trim(),
                lastNamePaternal: p.lastNamePaternal.trim(),
                lastNameMaternal: text(p.lastNameMaternal),
                birthDate: null,
                birthState: null,
                gender: null,
                isForeign: false,
                curp: text(p.curp),
                rfc: text(p.rfc),
                nss: null,
                mobilePhone: text(p.mobilePhone),
                email: text(p.email),
                street: null,
                extNumber: null,
                neighborhood: null,
                postalCode: null,
                municipality: null,
                state: null,
                source: v.source,
                notes: null,
              }
            : null,
        jobOpeningId: v.jobOpeningId!,
        source: v.source,
        offeredSalary: text(v.offeredSalary),
        comments: text(v.comments),
        capturedAt: null,
      });
      this.snack.open(`${created.person.fullName} agregado a ${created.openingName}`, undefined, { duration: 5000 });
      this.ref.close(created);
    });
  }
}
