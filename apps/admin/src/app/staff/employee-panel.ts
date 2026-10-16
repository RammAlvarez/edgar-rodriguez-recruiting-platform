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
import { Router } from '@angular/router';
import { curp, type DateKey, decimalString, mobilePhone, todayKey } from '@platform/shared';
import { API_CLIENT, zodValidator } from '@platform/ui';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { z } from 'zod';
import type { BankAccountType, ConversionDto, EmployeeDetailDto, PersonSummaryDto, RegistrationBank } from '../data/api-types';
import { ACCOUNT_TYPES, REGISTRATION_BANK } from '../data/payroll-labels';
import { EmployeesRepository } from '../data/payroll-repositories';
import { PeopleRepository } from '../data/recruiting-repositories';
import { CatalogsRepository } from '../data/repositories';
import { PanelState } from '../clients/panel-base';

export interface EmployeePanelData {
  /** alta: from Personal · convert: «Convertir en empleado» of a Contratado · rehire: «Reingreso» after a baja. */
  readonly mode: 'alta' | 'convert' | 'rehire';
  readonly conversion?: ConversionDto;
  readonly employee?: EmployeeDetailDto;
}

const hourly = z.string().trim().regex(/^\d{1,6}(\.\d{1,4})?$/, 'Importe con hasta 4 decimales.');

/**
 * «Alta de empleado» (SPEC §5.1): the person (found in the registry or captured), the grupo de nómina and puesto, turno,
 * fecha de ingreso, optional own rates, and how they will be paid — their account now, or a card requested at Banorte or
 * Banregio (the «Tarjetas por solicitar» queue, SPEC §5.5).
 */
@Component({
  selector: 'app-employee-panel',
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
    <h2 mat-dialog-title>{{ title }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="employee-form" novalidate>
        <h3 class="section-title">Persona</h3>
        @if (fixedPerson; as p) {
          <p class="fixed"><strong>{{ p.fullName }}</strong>@if (data.conversion; as c) { <span class="muted"> · {{ c.openingName }}</span> }</p>
        } @else {
          <mat-button-toggle-group [value]="personMode()" (change)="personMode.set($event.value)" aria-label="Persona" hideSingleSelectionIndicator>
            <mat-button-toggle value="existing">Buscar persona</mat-button-toggle>
            <mat-button-toggle value="new">Crear nueva persona</mat-button-toggle>
          </mat-button-toggle-group>
          @if (personMode() === 'existing') {
            <mat-form-field>
              <mat-label>Nombre, CURP o celular</mat-label>
              <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
              <input matInput [formControl]="search" [matAutocomplete]="people" autocomplete="off" />
              <mat-autocomplete #people="matAutocomplete" [displayWith]="personName" (optionSelected)="person.set($event.option.value)">
                @for (p of matches(); track p.id) {
                  <mat-option [value]="p">{{ p.fullName }}@if (p.curp) { <span class="muted"> · {{ p.curp }}</span> }</mat-option>
                }
              </mat-autocomplete>
              @if (person()) {
                <mat-hint>Seleccionada: {{ person()!.fullName }}</mat-hint>
              }
            </mat-form-field>
          } @else {
            <div formGroupName="person" class="row">
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
              </mat-form-field>
              <mat-form-field>
                <mat-label>CURP</mat-label>
                <input matInput formControlName="curp" autocomplete="off" />
                <mat-error>{{ s.msg('person.curp') }}</mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Celular</mat-label>
                <input matInput formControlName="mobilePhone" inputmode="tel" autocomplete="off" />
                <mat-error>{{ s.msg('person.mobilePhone') }}</mat-error>
              </mat-form-field>
            </div>
          }
        }

        <h3 class="section-title">Asignación</h3>
        <mat-form-field>
          <mat-label>Grupo de nómina</mat-label>
          <mat-select formControlName="payrollGroupId">
            @for (g of groups(); track g.id) {
              <mat-option [value]="g.id">{{ g.clientName }} — {{ g.name }}</mat-option>
            }
          </mat-select>
          @if (data.conversion && !groups().length) {
            <mat-hint>{{ data.conversion.openingName.split(' – ')[0] }} no tiene grupos de nómina: créalo en Clientes (nómina).</mat-hint>
          }
          <mat-error>{{ s.msg('payrollGroupId') }}</mat-error>
        </mat-form-field>
        <div class="row">
          <mat-form-field>
            <mat-label>Puesto</mat-label>
            <mat-select formControlName="positionId">
              @for (p of positions(); track p.id) {
                <mat-option [value]="p.id">{{ p.name }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ s.msg('positionId') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Turno</mat-label>
            <input matInput formControlName="shift" [matAutocomplete]="shifts" autocomplete="off" />
            <mat-autocomplete #shifts="matAutocomplete">
              @for (t of options.value()?.shifts ?? []; track t) {
                <mat-option [value]="t">{{ t }}</mat-option>
              }
            </mat-autocomplete>
            <mat-error>{{ s.msg('shift') }}</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field>
          <mat-label>Fecha de ingreso</mat-label>
          <input matInput type="date" formControlName="hiredAt" />
          <mat-error>{{ s.msg('hiredAt') }}</mat-error>
        </mat-form-field>
        @if (selectedPosition()?.rates; as r) {
          <p class="muted rates">Tarifas del puesto: SD bolsillo $ {{ r.dailySalaryPocket }} · SD IMSS $ {{ r.dailySalaryImss }} · hora $ {{ r.hourlyRate }}</p>
        }
        @if (data.mode === 'alta') {
          <details class="overrides">
            <summary>Tarifas propias del empleado (opcional)</summary>
            <div class="row">
              <mat-form-field>
                <mat-label>SD bolsillo</mat-label>
                <input matInput formControlName="pocket" inputmode="decimal" autocomplete="off" />
                <mat-error>{{ s.msg('pocket') }}</mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Costo por hora</mat-label>
                <input matInput formControlName="hourly" inputmode="decimal" autocomplete="off" />
                <mat-error>{{ s.msg('hourly') }}</mat-error>
              </mat-form-field>
            </div>
          </details>
        }

        @if (!personHasAccount) {
          <h3 class="section-title">Pago</h3>
          <mat-button-toggle-group [value]="payMode()" (change)="payMode.set($event.value)" aria-label="Pago" hideSingleSelectionIndicator>
            <mat-button-toggle value="card">Solicitar tarjeta</mat-button-toggle>
            <mat-button-toggle value="account">Ya tiene cuenta</mat-button-toggle>
          </mat-button-toggle-group>
          @if (payMode() === 'card') {
            <mat-form-field>
              <mat-label>Banco de la tarjeta</mat-label>
              <mat-select formControlName="cardBank">
                @for (b of cardBanks; track b.value) {
                  <mat-option [value]="b.value">{{ b.label }}</mat-option>
                }
              </mat-select>
              <mat-hint>Queda en «Tarjetas por solicitar» de Prenómina</mat-hint>
              <mat-error>{{ s.msg('cardBank') }}</mat-error>
            </mat-form-field>
          } @else {
            <div formGroupName="account">
              <div class="row">
                <mat-form-field>
                  <mat-label>Banco</mat-label>
                  <mat-select formControlName="bankId">
                    @for (b of banks(); track b.id) {
                      <mat-option [value]="b.id">{{ b.name }}</mat-option>
                    }
                  </mat-select>
                  <mat-error>{{ s.msg('account.bankId') }}</mat-error>
                </mat-form-field>
                <mat-form-field>
                  <mat-label>Tipo</mat-label>
                  <mat-select formControlName="accountType">
                    @for (t of accountTypes; track t.value) {
                      <mat-option [value]="t.value">{{ t.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              </div>
              <mat-form-field>
                <mat-label>Número</mat-label>
                <input matInput formControlName="accountNumber" inputmode="numeric" autocomplete="off" />
                <mat-hint>{{ accountHint() }}</mat-hint>
                <mat-error>{{ s.msg('account.accountNumber') ?? s.msg('bankAccount.accountNumber') }}</mat-error>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Titular (si no es el empleado)</mat-label>
                <input matInput formControlName="holderName" autocomplete="off" />
              </mat-form-field>
            </div>
          }
        } @else {
          <p class="muted">Ya tiene cuenta principal; no se solicita tarjeta.</p>
        }
        @if (s.alert()) {
          <p class="form-alert" role="alert">{{ s.alert() }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="employee-form" [disabled]="s.busy()">{{ action }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .row {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      mat-form-field {
        flex: 1 1 160px;
      }
    }
    .fixed {
      margin: 0 0 8px;
    }
    .rates {
      margin: -8px 0 8px;
      font: var(--mat-sys-body-small);
    }
    .overrides {
      margin-bottom: 12px;
      summary {
        cursor: pointer;
        margin-bottom: 8px;
        color: var(--mat-sys-primary);
      }
    }
    mat-button-toggle-group {
      margin-bottom: 12px;
    }
  `,
})
export class EmployeePanel {
  readonly data = inject<EmployeePanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<EmployeePanel>);
  private readonly employees = inject(EmployeesRepository);
  private readonly peopleRepo = inject(PeopleRepository);
  private readonly api = inject(API_CLIENT);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly catalogs = inject(CatalogsRepository).catalogs();
  readonly options = this.employees.options();

  readonly title = { alta: 'Alta de empleado', convert: 'Convertir en empleado', rehire: 'Reingreso' }[this.data.mode];
  readonly action = { alta: 'Dar de alta', convert: 'Convertir', rehire: 'Registrar reingreso' }[this.data.mode];
  readonly fixedPerson = this.data.conversion
    ? { id: this.data.conversion.personId, fullName: this.data.conversion.fullName }
    : this.data.employee
      ? { id: this.data.employee.personId, fullName: this.data.employee.fullName }
      : null;
  readonly personHasAccount = this.data.conversion?.hasAccount ?? this.data.employee?.hasAccount ?? false;

  readonly personMode = signal<'existing' | 'new'>('existing');
  readonly person = signal<PersonSummaryDto | null>(null);
  readonly payMode = signal<'card' | 'account'>('card');
  readonly cardBanks = (Object.keys(REGISTRATION_BANK) as RegistrationBank[]).map((value) => ({ value, label: REGISTRATION_BANK[value] }));
  readonly accountTypes = ACCOUNT_TYPES;

  private readonly fb = inject(FormBuilder);
  readonly search = this.fb.nonNullable.control('');
  readonly form = this.fb.nonNullable.group({
    person: this.fb.nonNullable.group({
      firstNames: ['', [Validators.required, Validators.maxLength(80)]],
      lastNamePaternal: ['', [Validators.required, Validators.maxLength(60)]],
      lastNameMaternal: ['', Validators.maxLength(60)],
      curp: ['', zodValidator(curp)],
      mobilePhone: ['', zodValidator(mobilePhone)],
    }),
    payrollGroupId: [null as string | null, Validators.required],
    positionId: [null as string | null, Validators.required],
    shift: ['C/DÍA', [Validators.required, Validators.maxLength(30)]],
    hiredAt: [(this.data.conversion?.hiredAt ?? todayKey()) as string, Validators.required],
    pocket: ['', zodValidator(decimalString())],
    hourly: ['', zodValidator(hourly)],
    cardBank: [(this.data.conversion?.suggestedBank ?? 'banorte') as RegistrationBank | null],
    account: this.fb.nonNullable.group({
      bankId: [null as string | null, Validators.required],
      accountType: ['debit_card' as BankAccountType],
      accountNumber: ['', Validators.required],
      holderName: ['', Validators.maxLength(40)],
    }),
  });
  readonly s = new PanelState(this.form);

  private readonly groupId = toSignal(this.form.controls.payrollGroupId.valueChanges, { initialValue: null });
  private readonly positionId = toSignal(this.form.controls.positionId.valueChanges, { initialValue: null });
  private readonly accountType = toSignal(this.form.controls.account.controls.accountType.valueChanges, { initialValue: 'debit_card' as BankAccountType });

  /** A conversion offers only the vacante's client's groups. */
  readonly groups = computed(() =>
    (this.options.value()?.groups ?? []).filter((g) => !this.data.conversion || g.clientId === this.data.conversion.clientId),
  );
  readonly positions = computed(() => this.groups().find((g) => g.id === this.groupId())?.positions ?? []);
  readonly selectedPosition = computed(() => this.positions().find((p) => p.id === this.positionId()));
  readonly banks = computed(() => (this.catalogs.value()?.banks ?? []).filter((b) => b.isActive));
  readonly accountHint = computed(() => ACCOUNT_TYPES.find((t) => t.value === this.accountType())?.hint ?? '');

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

  save(): Promise<void> {
    const f = this.form.controls;
    const toggle = (c: { enable: (o: object) => void; disable: (o: object) => void }, on: boolean) =>
      on ? c.enable({ emitEvent: false }) : c.disable({ emitEvent: false });
    toggle(f.person, !this.fixedPerson && this.personMode() === 'new');
    toggle(f.account, !this.personHasAccount && this.payMode() === 'account');
    toggle(f.cardBank, !this.personHasAccount && this.payMode() === 'card');
    if (!this.fixedPerson && this.personMode() === 'existing' && !this.person()) {
      this.s.alert.set('Busca y elige a la persona, o captura una nueva.');
      return Promise.resolve();
    }
    return this.s.run(async () => {
      const v = this.form.getRawValue();
      const text = (x: string | null | undefined) => (x == null || x.trim() === '' ? null : x.trim());
      let personId = this.fixedPerson?.id ?? this.person()?.id ?? null;
      if (!personId) {
        const p = v.person;
        const created = await this.api.post<{ id: string }>('/v1/people', {
          firstNames: p.firstNames.trim(), lastNamePaternal: p.lastNamePaternal.trim(), lastNameMaternal: text(p.lastNameMaternal),
          birthDate: null, birthState: null, gender: null, isForeign: false, curp: text(p.curp), rfc: null, nss: null,
          mobilePhone: text(p.mobilePhone), email: null, street: null, extNumber: null, neighborhood: null, postalCode: null,
          municipality: null, state: null, source: null, notes: null,
        });
        personId = created.id;
        // A retry after an error below must not create the person twice.
        this.person.set({ id: created.id, fullName: `${p.lastNamePaternal} ${p.firstNames}`, curp: null, rfc: null, mobilePhone: null, email: null });
        this.personMode.set('existing');
      }
      const account = !this.personHasAccount && this.payMode() === 'account'
        ? {
            bankId: v.account.bankId!, accountType: v.account.accountType, accountNumber: v.account.accountNumber,
            holderName: text(v.account.holderName), alias: null, validFrom: null, makePrimary: true,
          }
        : null;
      const cardBank = !this.personHasAccount && this.payMode() === 'card' ? v.cardBank : null;
      const saved =
        this.data.mode === 'rehire'
          ? await this.employees.rehire(this.data.employee!.id, {
              payrollGroupId: v.payrollGroupId!, positionId: v.positionId!, shift: v.shift, hiredAt: v.hiredAt as DateKey, cardBank,
            })
          : await this.employees.create({
              personId, payrollGroupId: v.payrollGroupId!, positionId: v.positionId!, shift: v.shift, hiredAt: v.hiredAt as DateKey,
              dailySalaryPocketOverride: text(v.pocket), dailySalaryImssOverride: null, hourlyRateOverride: text(v.hourly),
              daysWorkedBonusOverride: null, notes: null, cardBank, candidateId: this.data.conversion?.candidateId ?? null,
              bankAccount: account,
            });
      this.snack.open(`${saved.fullName} · ${saved.clientName} ${saved.positionName}`, undefined, { duration: 5000 });
      this.ref.close(saved);
      void this.router.navigate(['/personal', saved.id]);
    });
  }
}
