import { inject, Injectable } from '@angular/core';
import type { DateKey } from '@platform/shared';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type {
  AttendanceDayDto,
  AttendanceEntryRequest,
  AttendanceRecordDto,
  AttendanceSiteDto,
  BankAccountDto,
  BankAccountRequest,
  CalendarDayDto,
  CardQueueItemDto,
  IncidentDto,
  IncidentRequest,
  PayrollPeriodDto,
  PrepayrollExport,
  PrepayrollGridDto,
  PrepayrollLineDetailDto,
  ReportExportDto,
  ConversionDto,
  EmployeeDetailDto,
  EmployeeListItemDto,
  EmployeeOptionsDto,
  EmployeePickDto,
  EmployeeRequest,
  GeneratedPaymentFilesDto,
  GeneratedRegistrationDto,
  PayingAccountDto,
  PayrollReport,
  PayrollReportDto,
  PayrollReportOptionsDto,
  ReportFormat,
  PayingAccountRequest,
  PaymentBatchDto,
  PaymentFile,
  PeriodPaymentsDto,
  RegistrationBatchDto,
  RegistrationBatchRequest,
  RegistrationPreviewDto,
  GroupConceptRequest,
  PayrollGroupDetailDto,
  PayrollGroupListItemDto,
  PayrollGroupRequest,
  PositionRatesRequest,
  PositionRequest,
  RegistrationBank,
  StatusPeriodRequest,
  UpdateBankAccountRequest,
  UnlockAttendanceRequest,
  UpdateEmployeeRequest,
  WorkScheduleDto,
  WorkScheduleRequest,
} from './api-types';

/** Grupos de nómina, puestos and rates (Clientes (nómina) → detail, grant payroll_prepayroll). */
@Injectable({ providedIn: 'root' })
export class PayrollGroupsRepository {
  private readonly api = inject(API_CLIENT);

  list(clientId: () => string) {
    return liveQuery(() => this.api.get<PayrollGroupListItemDto[]>('/v1/payroll/groups', { clientId: clientId() }), ['payroll-groups']);
  }

  detail(id: () => string) {
    return liveQuery(() => this.api.get<PayrollGroupDetailDto>(`/v1/payroll/groups/${id()}`), ['payroll-groups']);
  }

  get(id: string): Promise<PayrollGroupDetailDto> {
    return this.api.get(`/v1/payroll/groups/${id}`);
  }

  save(id: string | null, body: PayrollGroupRequest): Promise<PayrollGroupDetailDto> {
    return id ? this.api.put(`/v1/payroll/groups/${id}`, body) : this.api.post('/v1/payroll/groups', body);
  }

  addPosition(groupId: string, body: PositionRequest): Promise<PayrollGroupDetailDto> {
    return this.api.post(`/v1/payroll/groups/${groupId}/positions`, body);
  }

  updatePosition(id: string, name: string, isActive: boolean): Promise<PayrollGroupDetailDto> {
    return this.api.put(`/v1/payroll/positions/${id}`, { name, isActive });
  }

  changeRates(id: string, body: PositionRatesRequest): Promise<PayrollGroupDetailDto> {
    return this.api.post(`/v1/payroll/positions/${id}/rates`, body);
  }

  saveConcepts(groupId: string, concepts: GroupConceptRequest[]): Promise<PayrollGroupDetailDto> {
    return this.api.put(`/v1/payroll/groups/${groupId}/concepts`, { concepts });
  }
}

export interface EmployeeFilters {
  readonly clientId?: string | null;
  readonly includeTerminated?: boolean;
}

/** Personal (grant payroll_staff): employees, Cambiar estatus, cuentas de pago, horarios. */
@Injectable({ providedIn: 'root' })
export class EmployeesRepository {
  private readonly api = inject(API_CLIENT);

  list(filters: () => EmployeeFilters) {
    return liveQuery(() => {
      const f = filters();
      return this.api.get<EmployeeListItemDto[]>('/v1/employees', {
        clientId: f.clientId ?? null,
        includeTerminated: f.includeTerminated ? 'true' : null,
      });
    }, ['employees']);
  }

  detail(id: () => string) {
    return liveQuery(() => this.api.get<EmployeeDetailDto>(`/v1/employees/${id()}`), ['employees', 'card-requests']);
  }

  options() {
    return liveQuery(() => this.api.get<EmployeeOptionsDto>('/v1/employees/options'), ['payroll-groups']);
  }

  conversion(candidateId: string): Promise<ConversionDto> {
    return this.api.get(`/v1/employees/conversion/${candidateId}`);
  }

  create(body: EmployeeRequest): Promise<EmployeeDetailDto> {
    return this.api.post('/v1/employees', body);
  }

  update(id: string, body: UpdateEmployeeRequest): Promise<EmployeeDetailDto> {
    return this.api.put(`/v1/employees/${id}`, body);
  }

  changeAssignment(id: string, payrollGroupId: string, positionId: string, shift: string): Promise<EmployeeDetailDto> {
    return this.api.post(`/v1/employees/${id}/assignment`, { payrollGroupId, positionId, shift });
  }

  rehire(id: string, body: { payrollGroupId: string; positionId: string; shift: string; hiredAt: DateKey; cardBank: RegistrationBank | null }): Promise<EmployeeDetailDto> {
    return this.api.post(`/v1/employees/${id}/rehire`, body);
  }

  addStatus(id: string, body: StatusPeriodRequest): Promise<EmployeeDetailDto> {
    return this.api.post(`/v1/employees/${id}/status`, body);
  }

  updateStatus(id: string, periodId: string, endDate: DateKey, reference: string | null): Promise<EmployeeDetailDto> {
    return this.api.put(`/v1/employees/${id}/status/${periodId}`, { endDate, reference });
  }

  endStatus(id: string, periodId: string): Promise<EmployeeDetailDto> {
    return this.api.post(`/v1/employees/${id}/status/${periodId}/end`);
  }

  requestCard(id: string, bank: RegistrationBank): Promise<EmployeeDetailDto> {
    return this.api.post(`/v1/employees/${id}/card-request`, { bank });
  }

  accounts(id: () => string) {
    return liveQuery(() => this.api.get<BankAccountDto[]>(`/v1/employees/${id()}/bank-accounts`), ['employees']);
  }

  addAccount(id: string, body: BankAccountRequest): Promise<BankAccountDto[]> {
    return this.api.post(`/v1/employees/${id}/bank-accounts`, body);
  }

  updateAccount(id: string, accountId: string, body: UpdateBankAccountRequest): Promise<BankAccountDto[]> {
    return this.api.put(`/v1/employees/${id}/bank-accounts/${accountId}`, body);
  }

  makePrimary(id: string, accountId: string): Promise<BankAccountDto[]> {
    return this.api.post(`/v1/employees/${id}/bank-accounts/${accountId}/primary`);
  }

  schedules(id: () => string) {
    return liveQuery(() => this.api.get<WorkScheduleDto[]>(`/v1/employees/${id()}/schedules`), ['schedules', 'employees']);
  }

  saveSchedule(id: string, scheduleId: string | null, body: WorkScheduleRequest): Promise<WorkScheduleDto[]> {
    return scheduleId ? this.api.put(`/v1/employees/${id}/schedules/${scheduleId}`, body) : this.api.post(`/v1/employees/${id}/schedules`, body);
  }

  deleteSchedule(id: string, scheduleId: string): Promise<WorkScheduleDto[]> {
    return this.api.delete(`/v1/employees/${id}/schedules/${scheduleId}`);
  }
}

/** Calendario de servicios (grant payroll_attendance). */
@Injectable({ providedIn: 'root' })
export class CalendarRepository {
  private readonly api = inject(API_CLIENT);

  range(range: () => { from: DateKey; to: DateKey; clientId: string | null }) {
    return liveQuery(() => {
      const r = range();
      return this.api.get<CalendarDayDto[]>('/v1/calendar', { from: r.from, to: r.to, clientId: r.clientId });
    }, ['schedules', 'employees']);
  }
}

/** Asistencia (grant payroll_attendance): capture by site and date, and the unlock of a closed day. */
@Injectable({ providedIn: 'root' })
export class AttendanceRepository {
  private readonly api = inject(API_CLIENT);

  sites(date: () => DateKey) {
    return liveQuery(() => this.api.get<AttendanceSiteDto[]>('/v1/attendance/sites', { date: date() }), ['attendance', 'schedules']);
  }

  /** Resolves to undefined until a site is chosen. */
  day(query: () => { siteId: string | null; date: DateKey }) {
    return liveQuery(() => {
      const q = query();
      return q.siteId
        ? this.api.get<AttendanceDayDto | undefined>('/v1/attendance/day', { siteId: q.siteId, date: q.date })
        : Promise.resolve(undefined);
    }, ['attendance', 'employees']);
  }

  save(siteId: string, date: DateKey, entries: AttendanceEntryRequest[]): Promise<AttendanceDayDto> {
    return this.api.put('/v1/attendance/day', { siteId, date, entries });
  }

  unlock(body: UnlockAttendanceRequest): Promise<AttendanceRecordDto> {
    return this.api.post('/v1/attendance/unlock', body);
  }

  employeeDays(id: () => string, from: DateKey, to: DateKey) {
    return liveQuery(() => this.api.get<AttendanceRecordDto[]>(`/v1/employees/${id()}/attendance`, { from, to }), ['attendance']);
  }
}

/** Prenómina (grant payroll_prepayroll): periods, the grid, a row's desglose, ajustes, comments, Cerrar captura and the files. */
@Injectable({ providedIn: 'root' })
export class PrepayrollRepository {
  private readonly api = inject(API_CLIENT);

  periods() {
    return liveQuery(() => this.api.get<PayrollPeriodDto[]>('/v1/payroll/periods'), ['prepayroll']);
  }

  /** Resolves to undefined until a period is chosen. */
  grid(id: () => string | null) {
    return liveQuery(() => {
      const periodId = id();
      return periodId ? this.api.get<PrepayrollGridDto | undefined>(`/v1/payroll/periods/${periodId}`) : Promise.resolve(undefined);
    }, ['prepayroll']);
  }

  line(id: () => string) {
    return liveQuery(() => this.api.get<PrepayrollLineDetailDto>(`/v1/payroll/lines/${id()}`), ['prepayroll', 'incidents']);
  }

  closeCapture(periodId: string): Promise<PrepayrollGridDto> {
    return this.api.post(`/v1/payroll/periods/${periodId}/close-capture`, {});
  }

  adjust(lineId: string, column: string, amount: string, reason: string): Promise<PrepayrollLineDetailDto> {
    return this.api.post(`/v1/payroll/lines/${lineId}/adjustments`, { column, amount, reason });
  }

  setComments(lineId: string, comments: string | null): Promise<void> {
    return this.api.put(`/v1/payroll/lines/${lineId}/comments`, { comments });
  }

  export(periodId: string, kind: PrepayrollExport): Promise<ReportExportDto> {
    return this.api.post(`/v1/payroll/periods/${periodId}/exports`, { kind });
  }

  cardQueue() {
    return liveQuery(() => this.api.get<CardQueueItemDto[]>('/v1/card-requests'), ['card-requests', 'employees']);
  }
}

/** Incidencias (grant payroll_incidents): horas extra, retroactivos, descuentos, abonos a préstamo. */
@Injectable({ providedIn: 'root' })
export class IncidentsRepository {
  private readonly api = inject(API_CLIENT);

  periods() {
    return liveQuery(() => this.api.get<PayrollPeriodDto[]>('/v1/payroll/incidents/periods'), ['prepayroll']);
  }

  employees(periodId: () => string | null) {
    return liveQuery(() => {
      const id = periodId();
      return id ? this.api.get<EmployeePickDto[]>('/v1/payroll/incidents/employees', { periodId: id }) : Promise.resolve([] as EmployeePickDto[]);
    }, ['employees']);
  }

  list(periodId: () => string | null) {
    return liveQuery(() => {
      const id = periodId();
      return id ? this.api.get<IncidentDto[]>('/v1/payroll/incidents', { periodId: id }) : Promise.resolve([] as IncidentDto[]);
    }, ['incidents', 'prepayroll']);
  }

  save(id: string | null, body: IncidentRequest): Promise<IncidentDto> {
    return id ? this.api.put(`/v1/payroll/incidents/${id}`, body) : this.api.post('/v1/payroll/incidents', body);
  }

  remove(id: string): Promise<void> {
    return this.api.delete(`/v1/payroll/incidents/${id}`);
  }
}

/**
 * Archivos de pago (grant payroll_payment_files): a period's payment panel, «Generar archivos de pago», downloads, «Subido al
 * banco», rechazos, the deposit date and the cuenta de cargo.
 */
@Injectable({ providedIn: 'root' })
export class PaymentsRepository {
  private readonly api = inject(API_CLIENT);

  panel(periodId: () => string) {
    return liveQuery(() => this.api.get<PeriodPaymentsDto>(`/v1/payroll/periods/${periodId()}/payments`), ['payments', 'prepayroll']);
  }

  generate(periodId: string): Promise<GeneratedPaymentFilesDto> {
    return this.api.post(`/v1/payroll/periods/${periodId}/payment-files`, {});
  }

  download(batchId: string, file: PaymentFile): Promise<ReportExportDto> {
    return this.api.post(`/v1/payroll/payment-batches/${batchId}/download`, { file });
  }

  uploaded(batchId: string): Promise<PaymentBatchDto> {
    return this.api.post(`/v1/payroll/payment-batches/${batchId}/uploaded`, {});
  }

  reject(rowId: string, reason: string): Promise<PaymentBatchDto> {
    return this.api.post(`/v1/payroll/payment-batch-rows/${rowId}/reject`, { reason });
  }

  setDepositDate(periodId: string, date: string | null): Promise<void> {
    return this.api.put(`/v1/payroll/periods/${periodId}/deposit-date`, { date });
  }

  /** Undefined until the owner types it (the API answers 204). */
  payingAccount() {
    return liveQuery(() => this.api.get<PayingAccountDto | undefined>('/v1/payroll/paying-account'), ['payments']);
  }

  setPayingAccount(body: PayingAccountRequest): Promise<PayingAccountDto> {
    return this.api.put('/v1/payroll/paying-account', body);
  }
}

/** Alta masiva files from the card queue (grant payroll_card_requests) and «Tarjeta recibida». */
@Injectable({ providedIn: 'root' })
export class CardBatchesRepository {
  private readonly api = inject(API_CLIENT);

  preview(bank: RegistrationBank, branchToSend: string | null): Promise<RegistrationPreviewDto> {
    return this.api.get('/v1/card-batches/preview', branchToSend ? { bank, branchToSend } : { bank });
  }

  generate(body: RegistrationBatchRequest): Promise<GeneratedRegistrationDto> {
    return this.api.post('/v1/card-batches', body);
  }

  list() {
    return liveQuery(() => this.api.get<RegistrationBatchDto[]>('/v1/card-batches'), ['card-batches']);
  }

  download(batchId: string): Promise<ReportExportDto> {
    return this.api.post(`/v1/card-batches/${batchId}/download`, {});
  }

  uploaded(batchId: string): Promise<RegistrationBatchDto> {
    return this.api.post(`/v1/card-batches/${batchId}/uploaded`, {});
  }

  receive(requestId: string, cardNumber: string): Promise<BankAccountDto> {
    return this.api.post(`/v1/card-requests/${requestId}/received`, { cardNumber });
  }
}

/** What the Reportes de nómina screen asks for: the report, Cliente (`null` = Todos) and a period or a range (PLAN D87). */
export interface PayrollReportQuery {
  readonly report: PayrollReport;
  readonly clientId: string | null;
  readonly periodId: string | null;
  readonly from: string | null;
  readonly to: string | null;
}

/** Reportes de nómina (grant payroll_prepayroll): the pickers, a report and its export. */
@Injectable({ providedIn: 'root' })
export class PayrollReportsRepository {
  private readonly api = inject(API_CLIENT);

  options() {
    return liveQuery(() => this.api.get<PayrollReportOptionsDto>('/v1/payroll-reports/options'), ['prepayroll', 'payroll-reports']);
  }

  report(query: () => PayrollReportQuery) {
    return liveQuery(() => {
      const q = query();
      const params: Record<string, string> = {};
      if (q.clientId) params['clientId'] = q.clientId;
      if (q.periodId) params['periodId'] = q.periodId;
      if (q.from) params['from'] = q.from;
      if (q.to) params['to'] = q.to;
      return this.api.get<PayrollReportDto>(`/v1/payroll-reports/${q.report}`, params);
    }, ['prepayroll', 'payments', 'incidents', 'employees', 'attendance', 'card-requests']);
  }

  export(q: PayrollReportQuery, format: ReportFormat): Promise<ReportExportDto> {
    return this.api.post('/v1/payroll-reports/exports', { ...q, format });
  }
}
