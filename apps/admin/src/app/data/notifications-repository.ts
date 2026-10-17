import { inject, Injectable } from '@angular/core';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type { NotificationsDto } from './api-types';

/** The bell (SPEC §3.5): the caller's own notifications. */
@Injectable({ providedIn: 'root' })
export class NotificationsRepository {
  private readonly api = inject(API_CLIENT);

  list() {
    return liveQuery(() => this.api.get<NotificationsDto>('/v1/notifications'), ['notifications']);
  }

  read(id: string): Promise<void> {
    return this.api.post(`/v1/notifications/${id}/read`, {});
  }

  readAll(): Promise<void> {
    return this.api.post('/v1/notifications/read-all', {});
  }
}
