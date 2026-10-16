import { ChangeDetectionStrategy, Component, inject, input, type OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardSubtitle, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { SignInError } from '@platform/api-client';
import { EMAIL_LINK_AUTH } from './api';

/**
 * Passwordless sign-in (candidates and clients apps). `/entrar` asks for the email and sends the link;
 * `/entrar/enlace` is where the link lands and completes the sign-in (asking the email again on another device).
 */
@Component({
  selector: 'ui-email-link-sign-in',
  imports: [ReactiveFormsModule, MatCard, MatCardHeader, MatCardTitle, MatCardSubtitle, MatCardContent, MatFormField, MatLabel, MatHint, MatError, MatInput, MatButton, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .sign-in { display: grid; place-items: center; min-height: 100%; padding: 16px; box-sizing: border-box; background: var(--mat-sys-surface-container-low); }
    .card { width: min(420px, 100%); }
    form { display: flex; flex-direction: column; gap: 8px; padding-top: 16px; }
    .message { margin: 0; }
    .error { color: var(--mat-sys-error); }
    button[type='submit'] { align-self: stretch; }
  `,
  template: `
    <main class="sign-in">
      <mat-card appearance="outlined" class="card">
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <mat-card-header>
          <mat-card-title>{{ appName() }}</mat-card-title>
          <mat-card-subtitle>Te enviaremos un enlace a tu correo para entrar, sin contraseña.</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          @if (sent()) {
            <p class="message" role="status">
              Te enviamos un enlace a <strong>{{ form.controls.email.value }}</strong>. Ábrelo en este dispositivo para entrar.
            </p>
            <button matButton type="button" (click)="sent.set(false)">Usar otro correo</button>
          } @else {
            <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
              <mat-form-field>
                <mat-label>Correo</mat-label>
                <input matInput type="email" formControlName="email" autocomplete="email" inputmode="email" />
                <mat-hint>El correo con el que te registraron</mat-hint>
                @if (form.controls.email.hasError('required')) {
                  <mat-error>El correo es obligatorio.</mat-error>
                } @else if (form.controls.email.hasError('email')) {
                  <mat-error>Introduce un correo válido.</mat-error>
                }
              </mat-form-field>
              @if (error()) {
                <p class="message error" role="alert">{{ error() }}</p>
              }
              <button matButton="filled" type="submit" [disabled]="busy()">
                {{ completing() ? 'Entrar' : 'Enviar enlace' }}
              </button>
            </form>
          }
        </mat-card-content>
      </mat-card>
    </main>
  `,
})
export class EmailLinkSignIn implements OnInit {
  private readonly auth = inject(EMAIL_LINK_AUTH);
  private readonly router = inject(Router);

  readonly appName = input('Plataforma de Reclutamiento');
  readonly form = inject(FormBuilder).nonNullable.group({ email: ['', [Validators.required, Validators.email]] });
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);
  readonly completing = signal(false);

  ngOnInit(): void {
    if (!this.auth.isSignInLink(location.href)) return;
    this.completing.set(true);
    if (this.auth.rememberedEmail()) void this.complete();
  }

  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    if (this.completing()) await this.complete(this.form.controls.email.value);
    else await this.run(async () => {
      await this.auth.sendSignInLink(this.form.controls.email.value);
      this.sent.set(true);
    });
  }

  private complete(email?: string): Promise<void> {
    return this.run(async () => {
      await this.auth.completeSignIn(location.href, email);
      await this.router.navigateByUrl('/');
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await work();
    } catch (e) {
      this.error.set(e instanceof SignInError ? e.message : 'No se pudo completar. Inténtalo de nuevo.');
    } finally {
      this.busy.set(false);
    }
  }
}
