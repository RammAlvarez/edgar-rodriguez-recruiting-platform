import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiError } from '@platform/api-client';
import type { MeDto, PermissionModule } from '@platform/shared';
import { API_CLIENT, AUTH_PROVIDER } from './api';

export type SessionState =
  | { readonly status: 'loading'; readonly message?: string }
  | { readonly status: 'signed-out' }
  | { readonly status: 'ready'; readonly me: MeDto }
  | { readonly status: 'denied'; readonly message: string };

/**
 * Who is signed in, as the API sees it (D36): provider sign-in state → `GET /v1/me` → role, grants, client scope.
 * The UI hides what the API would refuse, but the API is what enforces it.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(API_CLIENT);
  private readonly auth = inject(AUTH_PROVIDER);
  private readonly stateSignal = signal<SessionState>({ status: 'loading' });

  readonly state = this.stateSignal.asReadonly();
  readonly me = computed(() => {
    const s = this.stateSignal();
    return s.status === 'ready' ? s.me : null;
  });

  constructor() {
    let currentUid: string | null = null;
    this.auth.onChange((user) => {
      if (!user) {
        currentUid = null;
        this.stateSignal.set({ status: 'signed-out' });
        return;
      }
      // Token refreshes call back with the same user: keep the session; a new user must wait for /v1/me,
      // so guards see "loading" instead of a stale "signed-out".
      if (user.uid === currentUid) return;
      currentUid = user.uid;
      this.stateSignal.set({ status: 'loading' });
      void this.load(true);
    });
    this.api.cache.register(['me'], () => void this.load(false));
  }

  hasGrant(module: PermissionModule): boolean {
    const me = this.me();
    return !!me && me.role === 'administrator' && me.grants.some((g) => g.module === module);
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
  }

  /** A new sign-in (or app start) opens a session — last access, invitation accepted; later refreshes only read /v1/me. */
  private async load(start: boolean): Promise<void> {
    try {
      const me = start ? await this.api.post<MeDto>('/v1/me/sessions') : await this.api.get<MeDto>('/v1/me');
      this.stateSignal.set({ status: 'ready', me });
    } catch (e) {
      if (e instanceof ApiError && (e.isForbidden || e.isUnauthorized))
        this.stateSignal.set({ status: 'denied', message: e.message });
      // Too many requests (PLAN D92): stay loading and ask again when the API says.
      else if (e instanceof ApiError && e.isRateLimited) {
        this.stateSignal.set({ status: 'loading', message: e.message });
        setTimeout(() => void this.load(start), (e.retryAfterSeconds ?? 5) * 1000);
      }
      else throw e;
    }
  }
}
