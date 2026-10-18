import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { addDaysToKey, type DateKey, formatDateKey, todayKey } from '@platform/shared';
import { openSidePanel } from '@platform/ui';
import { DocumentChecklist, type ChecklistDocument, type ChecklistItem } from '@platform/ui/document-checklist';
import { DateKeyPipe, MoneyPipe } from '@platform/ui/pipes';
import type { BankAccountDto, EmployeeDetailDto, RegistrationBank, StatusPeriodDto, WorkScheduleDto } from '../data/api-types';
import { ACCOUNT_TYPE, ATTENDANCE, EMPLOYEE_STATUS, hourlyText, PERIOD_STATUS_LABEL, REGISTRATION_BANK, weekdaysText } from '../data/payroll-labels';
import { AttendanceRepository, EmployeesRepository } from '../data/payroll-repositories';
import { PeopleRepository } from '../data/recruiting-repositories';
import { CatalogsRepository } from '../data/repositories';
import { PersonPanel, type PersonPanelData } from '../candidates/person-panel';
import { prompt } from '../openings/prompt-dialog';
import { AccountPanel, type AccountPanelData } from './account-panel';
import { AssignmentPanel, type AssignmentPanelData } from './assignment-panel';
import { EmployeePanel, type EmployeePanelData } from './employee-panel';
import { SchedulePanel, type SchedulePanelData } from './schedule-panel';
import { StatusPanel, type StatusPanelData } from './status-panel';

/**
 * The employee (SPEC §5.1): the status pill with its dates and «Cambiar estatus» in the header; tabs Datos, Cuentas de pago,
 * Documentos (template «Empleado»), Horario, and Asistencia / Incidencias / Pagos, which fill in with S8–S10.
 */
@Component({
  selector: 'app-employee-detail',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatProgressBar, MatTabGroup, MatTab, DocumentChecklist, DateKeyPipe, MoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton routerLink="/personal" class="back"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>Personal</a>
      @if (employee.loading() && !employee.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (employee.error(); as err) {
        <p class="form-alert" role="alert">{{ err.message }}</p>
      }
      @if (employee.value(); as e) {
        <header class="page-header">
          <h1>
            <span class="title">
              {{ e.fullName }}
              <span class="pill" [class]="status[e.status].tone">{{ statusText(e) }}</span>
            </span>
            <span class="subtitle">{{ e.clientName }} — {{ e.groupName }} · {{ e.positionName }} · {{ e.shift }} · ingreso {{ e.hiredAt | dateKey }}</span>
            @if (e.nextPeriod && e.nextPeriod.id !== e.period?.id; as n) {
              <span class="subtitle">Próximo: {{ periodText(e.nextPeriod!) }}</span>
            }
          </h1>
          @if (e.status === 'terminated') {
            <button matButton="filled" type="button" (click)="rehire(e)"><mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Reingreso</button>
          } @else {
            <button matButton="outlined" type="button" (click)="changeStatus(e)"><mat-icon fontSet="material-symbols-outlined">edit</mat-icon>Cambiar estatus</button>
          }
        </header>

        <mat-tab-group animationDuration="0ms">
          <mat-tab label="Datos">
            <div class="grid">
              <section class="card">
                <div class="card-head">
                  <h2>Asignación</h2>
                  @if (e.status !== 'terminated') {
                    <button matButton type="button" (click)="assignment(e, 'assignment')">Cambiar cliente o puesto</button>
                  }
                </div>
                <dl>
                  <dt>Cliente</dt><dd>{{ e.clientName }}</dd>
                  <dt>Grupo de nómina</dt><dd>{{ e.groupName }}</dd>
                  <dt>Puesto</dt><dd>{{ e.positionName }}</dd>
                  <dt>Turno</dt><dd>{{ e.shift }}</dd>
                  <dt>Fecha de ingreso</dt><dd>{{ e.hiredAt | dateKey }}</dd>
                  @if (e.terminatedAt) {
                    <dt>Fecha de baja</dt><dd>{{ e.terminatedAt | dateKey }}@if (e.period?.reason) { · {{ e.period!.reason }} }</dd>
                  }
                </dl>
              </section>
              <section class="card">
                <div class="card-head">
                  <h2>Tarifas</h2>
                  @if (e.status !== 'terminated') {
                    <button matButton type="button" (click)="assignment(e, 'edit')">Editar</button>
                  }
                </div>
                @if (e.rates; as r) {
                  <dl>
                    <dt>SD bolsillo</dt><dd>{{ r.dailySalaryPocket | money }}@if (e.dailySalaryPocketOverride) { <span class="own">propia</span> }</dd>
                    <dt>SD IMSS</dt><dd>{{ r.dailySalaryImss | money }}@if (e.dailySalaryImssOverride) { <span class="own">propia</span> }</dd>
                    <dt>Costo por hora</dt><dd>$ {{ hourly(r.hourlyRate) }}@if (e.hourlyRateOverride) { <span class="own">propia</span> }</dd>
                    <dt>Bono días trabajados</dt><dd>{{ r.daysWorkedBonus | money }}@if (e.daysWorkedBonusOverride) { <span class="own">propia</span> }</dd>
                  </dl>
                } @else {
                  <p class="muted">El puesto no tiene tarifas vigentes hoy.</p>
                }
                @if (e.notes) {
                  <p class="notes">{{ e.notes }}</p>
                }
              </section>
              <section class="card">
                <div class="card-head">
                  <h2>Persona</h2>
                  <button matButton type="button" (click)="editPerson(e)">Editar</button>
                </div>
                <dl>
                  <dt>CURP</dt><dd class="mono">{{ e.curp ?? '—' }}</dd>
                  <dt>RFC</dt><dd class="mono">{{ e.rfc ?? '—' }}</dd>
                  <dt>NSS</dt><dd class="mono">{{ e.nss ?? '—' }}</dd>
                  <dt>Celular</dt><dd>{{ e.mobilePhone ?? '—' }}</dd>
                  <dt>Correo</dt><dd>{{ e.email ?? '—' }}</dd>
                  @if (e.isForeign) {
                    <dt>Extranjero</dt><dd>Sí</dd>
                  }
                </dl>
              </section>
            </div>
          </mat-tab>

          <mat-tab label="Cuentas de pago">
            <div class="tab-actions">
              <button matButton="filled" type="button" (click)="account(e, null)"><mat-icon fontSet="material-symbols-outlined">add</mat-icon>Agregar cuenta</button>
            </div>
            @if (!e.hasAccount) {
              @if (e.cardRequest; as r) {
                <p class="banner">Tarjeta solicitada · {{ bank[r.bank] }} — en «Tarjetas por solicitar» de Prenómina. Al recibirla se agrega aquí como principal.</p>
              } @else if (e.status !== 'terminated') {
                <p class="banner warn">
                  Sin cuenta principal: no se le puede pagar.
                  <button matButton type="button" [matMenuTriggerFor]="banks">Solicitar tarjeta</button>
                  <mat-menu #banks="matMenu">
                    @for (b of cardBanks; track b) {
                      <button mat-menu-item type="button" (click)="requestCard(e, b)">{{ bank[b] }}</button>
                    }
                  </mat-menu>
                </p>
              } @else {
                <p class="banner warn">
                  Sin cuenta principal. Si se le debe un pago, agrega una cuenta aquí o regístralo como «Pagar fuera de archivo» en Prenómina → Pagar.
                </p>
              }
            }
            <div class="list-card">
              @for (a of accounts.value() ?? []; track a.id) {
                <div class="list-row" [class.closed]="!a.isCurrent">
                  <div class="grow">
                    <div class="title">
                      {{ a.bankName }} · {{ type[a.accountType] }} <span class="mono">•••• {{ a.last4 }}</span>
                      @if (a.isPrimary) { <span class="pill positive">Principal</span> }
                      @if (!a.isCurrent || a.validTo) { <span class="pill">Cerrada</span> }
                    </div>
                    <div class="muted">
                      Titular: {{ a.holderName }}@if (a.isThirdParty) { <span class="pill">Tercero</span> }
                      @if (a.alias) { · {{ a.alias }} } · desde {{ a.validFrom | dateKey }}@if (a.validTo) { hasta {{ a.validTo | dateKey }} }
                    </div>
                  </div>
                  <button matIconButton type="button" [matMenuTriggerFor]="menu" aria-label="Acciones de la cuenta">
                    <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
                  </button>
                  <mat-menu #menu="matMenu">
                    @if (!a.isPrimary && !a.validTo) {
                      <button mat-menu-item type="button" (click)="makePrimary(e, a)">Hacer principal</button>
                    }
                    <button mat-menu-item type="button" (click)="account(e, a)">Editar titular o vigencia</button>
                  </mat-menu>
                </div>
              } @empty {
                @if (accounts.value()) {
                  <p class="list-empty">Sin cuentas de pago.</p>
                }
              }
            </div>
          </mat-tab>

          <mat-tab label="Documentos">
            @if (checklist.value(); as list) {
              <ui-document-checklist [checklist]="list" [canReview]="true" [uploading]="uploading()"
                (upload)="upload(e, $event.item, $event.file)" (view)="view($event)" (validate)="validate($event)" (reject)="reject($event)" />
            } @else if (checklist.error(); as err) {
              <p class="form-alert">{{ err.message }}</p>
            }
          </mat-tab>

          <mat-tab label="Horario">
            @if (e.status !== 'terminated') {
              <div class="tab-actions">
                <button matButton="filled" type="button" (click)="schedule(e, null)"><mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nuevo horario</button>
              </div>
            }
            <div class="list-card">
              @for (s of schedules.value() ?? []; track s.id) {
                <div class="list-row" [class.closed]="s.validTo && s.validTo < today">
                  <div class="grow">
                    <div class="title">{{ s.siteName }}@if (s.jobDescription) { · {{ s.jobDescription }} }</div>
                    <div class="muted">{{ when(s) }} · {{ s.startTime.slice(0, 5) }}–{{ s.endTime.slice(0, 5) }}</div>
                  </div>
                  <button matIconButton type="button" [matMenuTriggerFor]="smenu" aria-label="Acciones del horario">
                    <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
                  </button>
                  <mat-menu #smenu="matMenu">
                    <button mat-menu-item type="button" (click)="schedule(e, s)">Editar</button>
                    <button mat-menu-item type="button" (click)="removeSchedule(e, s)">Quitar</button>
                  </mat-menu>
                </div>
              } @empty {
                @if (schedules.value()) {
                  <p class="list-empty">Sin horario. El Calendario muestra a quién se espera en cada sitio.</p>
                }
              }
            </div>
          </mat-tab>

          <mat-tab label="Asistencia">
            <p class="muted range">Últimos 14 días</p>
            <div class="list-card">
              @for (a of days.value() ?? []; track a.id) {
                <div class="list-row">
                  <span class="date">{{ a.workDate | dateKey }}</span>
                  <span class="grow">
                    <span class="pill" [class]="attendance[a.status!].tone">{{ attendance[a.status!].label }}</span>
                    @if (a.hoursWorked) { <span class="muted"> {{ +a.hoursWorked }} h</span> }
                    @if (+a.overtimeHours > 0) { <span class="muted"> + {{ +a.overtimeHours }} extra</span> }
                    @if (a.siteName) { <span class="muted"> · {{ a.siteName }}</span> }
                    @if (a.isAutoAbsent) { <span class="muted"> · automática</span> }
                  </span>
                  @if (a.isLocked) { <mat-icon fontSet="material-symbols-outlined" class="muted" aria-label="Cerrado">lock</mat-icon> }
                </div>
              } @empty {
                @if (days.value()) {
                  <p class="list-empty">Sin asistencia registrada en estos días.</p>
                }
              }
            </div>
          </mat-tab>

          @for (t of later; track t.label) {
            <mat-tab [label]="t.label">
              <div class="empty-state">
                <mat-icon fontSet="material-symbols-outlined">{{ t.icon }}</mat-icon>
                <p>{{ t.text }}</p>
              </div>
            </mat-tab>
          }
        </mat-tab-group>
      }
    </div>
  `,
  styles: `
    .title {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
      padding-top: 16px;
    }
    .card {
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-medium);
      }
    }
    dl {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 6px 16px;
      margin: 12px 0 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
    }
    .own {
      margin-left: 6px;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-primary);
    }
    .notes {
      margin: 12px 0 0;
      white-space: pre-line;
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      padding: 12px 16px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
      &.warn {
        background: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
      }
    }
    .list-row.closed {
      opacity: 0.6;
    }
    .range {
      margin: 16px 0 8px;
    }
    .date {
      width: 96px;
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class EmployeeDetail {
  private readonly employeeId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly repo = inject(EmployeesRepository);
  private readonly people = inject(PeopleRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly catalogs = inject(CatalogsRepository).catalogs();
  private readonly options = this.repo.options();

  readonly employee = this.repo.detail(() => this.employeeId);
  readonly accounts = this.repo.accounts(() => this.employeeId);
  readonly schedules = this.repo.schedules(() => this.employeeId);
  readonly days = inject(AttendanceRepository).employeeDays(() => this.employeeId, addDaysToKey(todayKey(), -13), todayKey());
  readonly attendance = ATTENDANCE;
  private readonly personId = computed(() => this.employee.value()?.personId ?? null);
  private readonly templateId = computed(() => this.employee.value()?.documentTemplateId ?? null);
  readonly checklist = this.people.checklist(() => this.personId(), () => this.templateId());
  readonly uploading = signal<{ documentTypeId: string; progress: number } | null>(null);

  readonly status = EMPLOYEE_STATUS;
  readonly hourly = hourlyText;
  readonly bank = REGISTRATION_BANK;
  readonly type = ACCOUNT_TYPE;
  readonly cardBanks: RegistrationBank[] = ['banorte', 'banregio'];
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Monterrey' }).format(new Date());
  readonly later = [
    { label: 'Incidencias', icon: 'edit_note', text: 'Las incidencias llegan con la Prenómina.' },
    { label: 'Pagos', icon: 'payments', text: 'Los pagos aparecen cuando se generen los archivos de pago.' },
  ];

  constructor() {
    // The checklist needs the person and template, which arrive with the employee: load it once they are known.
    effect(() => {
      if (this.personId()) untracked(() => this.checklist.reload());
    });
  }

  periodText(p: StatusPeriodDto): string {
    const range = p.endDate ? `${formatDateKey(p.startDate as DateKey)} – ${formatDateKey(p.endDate as DateKey)}` : `desde ${formatDateKey(p.startDate as DateKey)}`;
    return `${PERIOD_STATUS_LABEL[p.status]} · ${range}`;
  }

  /** «Vacaciones · 14/10/2026 – 20/10/2026»; «Activo» alone. */
  statusText(e: EmployeeDetailDto): string {
    return e.period ? this.periodText(e.period) : EMPLOYEE_STATUS[e.status].label;
  }

  when(s: WorkScheduleDto): string {
    if (s.oneOffDate) return formatDateKey(s.oneOffDate as DateKey);
    const days = weekdaysText(s.weekdays.map(Number));
    return s.validTo ? `${days} hasta ${formatDateKey(s.validTo as DateKey)}` : days;
  }

  changeStatus(e: EmployeeDetailDto): void {
    openSidePanel<StatusPanel, StatusPanelData>(this.dialog, StatusPanel, {
      employee: e,
      terminationReasons: this.options.value()?.terminationReasons ?? [],
    });
  }

  /** The person's identity, contact and address (the card alta needs CURP, RFC and the address): the candidate's panel. */
  async editPerson(e: EmployeeDetailDto): Promise<void> {
    try {
      const person = await this.people.get(e.personId);
      openSidePanel<PersonPanel, PersonPanelData>(this.dialog, PersonPanel, { person });
    } catch (err) {
      this.snack.open(err instanceof ApiError ? err.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    }
  }

  rehire(e: EmployeeDetailDto): void {
    openSidePanel<EmployeePanel, EmployeePanelData>(this.dialog, EmployeePanel, { mode: 'rehire', employee: e });
  }

  assignment(e: EmployeeDetailDto, mode: AssignmentPanelData['mode']): void {
    const options = this.options.value();
    if (!options) return;
    openSidePanel<AssignmentPanel, AssignmentPanelData>(this.dialog, AssignmentPanel, { mode, employee: e, options });
  }

  account(e: EmployeeDetailDto, account: BankAccountDto | null): void {
    openSidePanel<AccountPanel, AccountPanelData>(this.dialog, AccountPanel, {
      employeeId: e.id,
      employeeName: e.fullName,
      banks: (this.catalogs.value()?.banks ?? []).filter((b) => b.isActive),
      account,
    });
  }

  schedule(e: EmployeeDetailDto, schedule: WorkScheduleDto | null): void {
    const sites = this.options.value()?.groups.find((g) => g.id === e.payrollGroupId)?.sites ?? [];
    openSidePanel<SchedulePanel, SchedulePanelData>(this.dialog, SchedulePanel, { employeeId: e.id, employeeName: e.fullName, sites, schedule });
  }

  removeSchedule(e: EmployeeDetailDto, s: WorkScheduleDto): Promise<void> {
    return this.attempt(() => this.repo.deleteSchedule(e.id, s.id), 'Horario quitado');
  }

  makePrimary(e: EmployeeDetailDto, a: BankAccountDto): Promise<void> {
    return this.attempt(() => this.repo.makePrimary(e.id, a.id), `${a.bankName} •••• ${a.last4} es la principal desde el siguiente período`);
  }

  requestCard(e: EmployeeDetailDto, bank: RegistrationBank): Promise<void> {
    return this.attempt(() => this.repo.requestCard(e.id, bank), `Tarjeta ${REGISTRATION_BANK[bank]} solicitada`);
  }

  async upload(e: EmployeeDetailDto, item: ChecklistItem, file: File): Promise<void> {
    this.uploading.set({ documentTypeId: item.documentTypeId, progress: 0 });
    try {
      await this.people.upload(e.personId, item, file, (progress) => this.uploading.set({ documentTypeId: item.documentTypeId, progress }));
      this.snack.open(`${item.nameEs} entregado`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(err instanceof ApiError ? err.message : 'No se pudo subir el archivo.', undefined, { duration: 6000 });
    } finally {
      this.uploading.set(null);
    }
  }

  async view(d: ChecklistDocument): Promise<void> {
    // Open the tab first (popup blockers allow it only inside the click), then point it at the presigned URL.
    const tab = window.open('', '_blank');
    try {
      const { url } = await this.people.url(d.id, false);
      if (tab) tab.location.href = url;
      else window.open(url, '_blank', 'noopener');
    } catch (err) {
      tab?.close();
      this.snack.open(err instanceof ApiError ? err.message : 'No se pudo abrir el documento.', undefined, { duration: 6000 });
    }
  }

  validate(d: ChecklistDocument): Promise<void> {
    return this.attempt(() => this.people.validate(d.id), 'Documento validado');
  }

  async reject(d: ChecklistDocument): Promise<void> {
    const reason = await prompt(this.dialog, { title: 'Rechazar documento', label: 'Motivo', confirm: 'Rechazar', kind: 'text', destructive: true, maxLength: 300 });
    if (reason) await this.attempt(() => this.people.reject(d.id, reason), 'Documento rechazado');
  }

  private async attempt(work: () => Promise<unknown>, done: string): Promise<void> {
    try {
      await work();
      this.snack.open(done, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(err instanceof ApiError ? err.message : 'Ocurrió un error inesperado.', undefined, { duration: 6000 });
    }
  }
}
