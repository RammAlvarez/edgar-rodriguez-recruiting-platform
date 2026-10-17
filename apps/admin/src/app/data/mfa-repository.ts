import { inject, Injectable } from '@angular/core';
import { API_CLIENT } from '@platform/ui';
import type { MfaRecoveryCodesDto, MfaSetupDto } from './api-types';

/** Verificación en dos pasos (PLAN D89): each call invalidates «me», so the session sees the new state. */
@Injectable({ providedIn: 'root' })
export class MfaRepository {
  private readonly api = inject(API_CLIENT);

  setup(): Promise<MfaSetupDto> {
    return this.api.post('/v1/me/mfa/setup', {});
  }

  enable(code: string): Promise<MfaRecoveryCodesDto> {
    return this.api.post('/v1/me/mfa/enable', { code });
  }

  verify(code: string): Promise<void> {
    return this.api.post('/v1/me/mfa/verify', { code });
  }

  disable(code: string): Promise<void> {
    return this.api.post('/v1/me/mfa/disable', { code });
  }

  reset(userId: string): Promise<void> {
    return this.api.post(`/v1/users/${userId}/mfa/reset`, {});
  }
}
