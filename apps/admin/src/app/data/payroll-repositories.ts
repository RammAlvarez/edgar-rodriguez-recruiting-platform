import { inject, Injectable } from '@angular/core';
import type { DateKey } from '@platform/shared';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type {
  BankAccountDto,
  BankAccountRequest,
  CalendarDayDto,
  ConversionDto,
  EmployeeDetailDto,
  EmployeeListItemDto,
  EmployeeOptionsDto,
  EmployeeRequest,
  GroupConceptRequest,
  PayrollGroupDetailDto,
  PayrollGroupListItemDto,
  PayrollGroupRequest,
  PositionRatesRequest,
  PositionRequest,
  RegistrationBank,
  StatusPeriodRequest,
  UpdateBankAccountRequest,
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
