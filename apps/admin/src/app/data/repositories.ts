import { inject, Injectable } from '@angular/core';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type {
  CatalogsDto,
  ClientDetailDto,
  ClientListItemDto,
  ContactRequest,
  CreateClientRequest,
  InviteResultDto,
  InviteUserRequest,
  SiteRequest,
  StatutoryHolidaysResult,
  UpdateClientRequest,
  UpdateUserRequest,
  UserDto,
} from './api-types';

/**
 * The admin app's calls to `/v1`. Queries are live (re-run on `X-Invalidates` and «Actualizar»); call the query methods
 * in an injection context (component field initializers).
 */
@Injectable({ providedIn: 'root' })
export class CatalogsRepository {
  private readonly api = inject(API_CLIENT);

  catalogs() {
    return liveQuery(() => this.api.get<CatalogsDto>('/v1/catalogs'), ['catalogs']);
  }

  /** `kind` is the route segment: zones, positions, education-levels, benefits, holidays, banks, document-types, document-templates. */
  save(kind: string, id: string | null, body: unknown): Promise<unknown> {
    return id ? this.api.put(`/v1/catalogs/${kind}/${id}`, body) : this.api.post(`/v1/catalogs/${kind}`, body);
  }

  deleteHoliday(id: string): Promise<void> {
    return this.api.delete(`/v1/catalogs/holidays/${id}`);
  }

  addStatutoryHolidays(year: number): Promise<StatutoryHolidaysResult> {
    return this.api.post('/v1/catalogs/holidays/statutory', { year });
  }
}

/** Both module entries (SPEC §3.3): the recruiting routes, and the payroll routes that honour the Prenómina grant's scope. */
@Injectable({ providedIn: 'root' })
export class ClientsRepository {
  private readonly api = inject(API_CLIENT);

  list(module: 'recruiting' | 'payroll') {
    return liveQuery(() => this.api.get<ClientListItemDto[]>(this.base(module)), ['clients']);
  }

  detail(module: 'recruiting' | 'payroll', id: () => string) {
    return liveQuery(() => this.api.get<ClientDetailDto>(`${this.base(module)}/${id()}`), ['clients', 'users']);
  }

  create(module: 'recruiting' | 'payroll', body: CreateClientRequest): Promise<ClientDetailDto> {
    return this.api.post(this.base(module), body);
  }

  update(id: string, body: UpdateClientRequest): Promise<void> {
    return this.api.put(`/v1/clients/${id}`, body);
  }

  saveContact(clientId: string, contactId: string | null, body: ContactRequest): Promise<ClientDetailDto> {
    return contactId
      ? this.api.put(`/v1/clients/${clientId}/contacts/${contactId}`, body)
      : this.api.post(`/v1/clients/${clientId}/contacts`, body);
  }

  deleteContact(clientId: string, contactId: string): Promise<ClientDetailDto> {
    return this.api.delete(`/v1/clients/${clientId}/contacts/${contactId}`);
  }

  saveSite(clientId: string, siteId: string | null, body: SiteRequest): Promise<ClientDetailDto> {
    return siteId ? this.api.put(`/v1/clients/${clientId}/sites/${siteId}`, body) : this.api.post(`/v1/clients/${clientId}/sites`, body);
  }

  private base(module: 'recruiting' | 'payroll'): string {
    return module === 'recruiting' ? '/v1/clients' : '/v1/payroll/clients';
  }
}

@Injectable({ providedIn: 'root' })
export class UsersRepository {
  private readonly api = inject(API_CLIENT);

  list() {
    return liveQuery(() => this.api.get<UserDto[]>('/v1/users'), ['users']);
  }

  invite(body: InviteUserRequest): Promise<InviteResultDto> {
    return this.api.post('/v1/users', body);
  }

  update(id: string, body: UpdateUserRequest): Promise<UserDto> {
    return this.api.put(`/v1/users/${id}`, body);
  }

  resendInvitation(id: string): Promise<InviteResultDto> {
    return this.api.post(`/v1/users/${id}/resend-invitation`);
  }
}
