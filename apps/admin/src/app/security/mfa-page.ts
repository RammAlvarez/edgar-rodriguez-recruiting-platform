import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { Session } from '@platform/ui/session';
import { toDataURL } from 'qrcode';
import type { MfaSetupDto } from '../data/api-types';
import { MfaRepository } from '../data/mfa-repository';
import { mfaPending } from './mfa-guard';

/**
 * Verificación en dos pasos (SPEC §2.3, PLAN D89): turn it on with an authenticator app (QR or the key by hand, a code, then the
 * recovery codes shown once); at each new sign-in, the code — or a recovery code — before any screen; turn it off with a code
 * when the account does not require it.
 */
@Component({
  selector: 'app-mfa-page',
  imports: [FormsModule, MatButton, MatFormField, MatHint, MatLabel, MatIcon, MatInput],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="wrap">
      <section class="card">
        <h1><mat-icon fontSet="material-symbols-outlined">shield_lock</mat-icon>Verificación en dos pasos</h1>
        @if (recovery(); as codes) {
          <p>Listo. Guarda estos códigos de recuperación en un lugar seguro: cada uno sirve <strong>una vez</strong> si no tienes tu teléfono. No se vuelven a mostrar.</p>
          <ul class="codes">
            @for (c of codes; track c) {
              <li>{{ c }}</li>
            }
          </ul>
          <div class="end"><button matButton="filled" type="button" (click)="leave()">Ya los guardé</button></div>
        } @else {
          @switch (mode()) {
            @case ('enroll') {
              @if (me()?.mfaRequired) {
                <p>Tu cuenta requiere este paso extra. Actívalo para continuar.</p>
              } @else {
                <p>Además de tu contraseña, te pediremos un código de 6 dígitos de una app de autenticación (Google Authenticator, Microsoft Authenticator, 1Password…).</p>
              }
              @if (setup(); as s) {
                <ol>
                  <li>En tu app agrega una cuenta y escanea este código:</li>
                </ol>
                @if (qr(); as image) {
                  <img class="qr" [src]="image" width="200" height="200" alt="Código QR para tu app de autenticación" />
                }
                <p class="muted">¿No puedes escanear? Escribe esta clave: <code class="key">{{ spaced(s.secret) }}</code></p>
                <ol start="2">
                  <li>Escribe el código de 6 dígitos que muestra la app.</li>
                </ol>
                <form (ngSubmit)="enable()">
                  <mat-form-field class="full">
                    <mat-label>Código</mat-label>
                    <input matInput name="code" [(ngModel)]="code" inputmode="numeric" autocomplete="one-time-code" maxlength="7" required />
                  </mat-form-field>
                  @if (error()) {
                    <p class="form-alert" role="alert">{{ error() }}</p>
                  }
                  <div class="end"><button matButton="filled" type="submit" [disabled]="busy()">Activar</button></div>
                </form>
              } @else {
                <div class="end">
                  @if (!me()?.mfaRequired) {
                    <button matButton type="button" (click)="leave()">Ahora no</button>
                  }
                  <button matButton="filled" type="button" (click)="start()" [disabled]="busy()">Generar código QR</button>
                </div>
              }
            }
            @case ('verify') {
              <p>Escribe el código de 6 dígitos de tu app de autenticación. Si no tienes el teléfono, usa uno de tus códigos de recuperación.</p>
              <form (ngSubmit)="verify()">
                <mat-form-field class="full">
                  <mat-label>Código</mat-label>
                  <input matInput name="code" [(ngModel)]="code" autocomplete="one-time-code" maxlength="11" required />
                  <mat-hint>6 dígitos, o un código de recuperación (XXXX-XXXX)</mat-hint>
                </mat-form-field>
                @if (error()) {
                  <p class="form-alert" role="alert">{{ error() }}</p>
                }
                <div class="end">
                  <button matButton type="button" (click)="signOut()">Cerrar sesión</button>
                  <button matButton="filled" type="submit" [disabled]="busy()">Continuar</button>
                </div>
              </form>
            }
            @case ('manage') {
              <p>Está activa: cada vez que inicies sesión te pediremos el código de tu app.</p>
              @if (me()?.mfaRequired) {
                <p class="muted">Tu cuenta la requiere; si perdiste el teléfono, pide a un administrador que la restablezca en Usuarios.</p>
                <div class="end"><button matButton="filled" type="button" (click)="leave()">Volver</button></div>
              } @else {
                <form (ngSubmit)="disable()">
                  <mat-form-field class="full">
                    <mat-label>Código para desactivarla</mat-label>
                    <input matInput name="code" [(ngModel)]="code" autocomplete="one-time-code" maxlength="11" />
                  </mat-form-field>
                  @if (error()) {
                    <p class="form-alert" role="alert">{{ error() }}</p>
                  }
                  <div class="end">
                    <button matButton type="button" (click)="leave()">Volver</button>
                    <button matButton="outlined" type="submit" [disabled]="busy()">Desactivar</button>
                  </div>
                </form>
              }
            }
          }
        }
      </section>
    </main>
  `,
  styles: `
    .wrap {
      min-height: 100dvh;
      display: grid;
      place-items: center;
      padding: 16px;
      box-sizing: border-box;
    }
    .card {
      width: min(440px, 100%);
      padding: 24px;
      border-radius: 16px;
      background: var(--mat-sys-surface-container);
      box-sizing: border-box;
    }
    h1 {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
    }
    .qr {
      display: block;
      margin: 8px auto;
      border-radius: 8px;
      background: #fff;
      padding: 8px;
    }
    .key {
      word-break: break-all;
      font-family: ui-monospace, monospace;
    }
    .codes {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 6px 16px;
      padding: 12px 16px;
      list-style: none;
      border-radius: 12px;
      background: var(--mat-sys-surface);
      font-family: ui-monospace, monospace;
    }
    .full {
      width: 100%;
    }
    .end {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 8px;
    }
    ol {
      padding-left: 20px;
      margin: 8px 0;
    }
  `,
})
export class MfaPage {
  private readonly session = inject(Session);
  private readonly repo = inject(MfaRepository);
  private readonly router = inject(Router);

  readonly me = this.session.me;
  readonly mode = computed(() => {
    const me = this.me();
    if (!me?.mfaEnabled) return 'enroll';
    return me.mfaVerified ? 'manage' : 'verify';
  });
  readonly setup = signal<MfaSetupDto | null>(null);
  readonly qr = signal<string | null>(null);
  readonly recovery = signal<string[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  private readonly leaving = signal(false);
  code = '';

  constructor() {
    // Leave once the session (reloaded by the API's «me» invalidation) no longer asks for the code.
    effect(() => {
      if (this.leaving() && !mfaPending(this.me())) void this.router.navigateByUrl('/');
    });
  }

  spaced(secret: string): string {
    return secret.replace(/(.{4})/g, '$1 ').trim();
  }

  async start(): Promise<void> {
    await this.run(async () => {
      const s = await this.repo.setup();
      this.setup.set(s);
      this.qr.set(await toDataURL(s.otpAuthUri, { margin: 0, width: 200 }));
    });
  }

  async enable(): Promise<void> {
    await this.run(async () => {
      const result = await this.repo.enable(this.code.trim());
      this.code = '';
      this.recovery.set(result.recoveryCodes as string[]);
    });
  }

  async verify(): Promise<void> {
    await this.run(async () => {
      await this.repo.verify(this.code.trim());
      this.code = '';
      this.leaving.set(true);
    });
  }

  async disable(): Promise<void> {
    await this.run(async () => {
      await this.repo.disable(this.code.trim());
      this.code = '';
      this.leaving.set(true);
    });
  }

  leave(): void {
    this.recovery.set(null);
    this.leaving.set(true);
    if (!mfaPending(this.me())) void this.router.navigateByUrl('/');
  }

  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await work();
    } catch (e) {
      this.error.set(e instanceof ApiError ? (e.fieldErrors ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message) : e.message) : 'No se pudo completar.');
    } finally {
      this.busy.set(false);
    }
  }
}
