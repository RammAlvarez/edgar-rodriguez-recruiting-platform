import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { PASSWORD_AUTH } from '../auth-token';
import { SignInError } from '@platform/api-client';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule, MatCard, MatCardHeader, MatCardTitle, MatCardContent, MatFormField, MatLabel, MatError, MatInput, MatButton, MatProgressBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './sign-in.scss',
  template: `
    <main class="sign-in">
      <mat-card appearance="outlined" class="card">
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <mat-card-header>
          <mat-card-title>Plataforma de Reclutamiento y Prenómina</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="signIn()" novalidate>
            <mat-form-field>
              <mat-label>Correo</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
              @if (form.controls.email.hasError('required')) {
                <mat-error>El correo es obligatorio.</mat-error>
              } @else if (form.controls.email.hasError('email')) {
                <mat-error>Introduce un correo válido.</mat-error>
              }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Contraseña</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="current-password" />
              @if (form.controls.password.hasError('required')) {
                <mat-error>La contraseña es obligatoria.</mat-error>
              }
            </mat-form-field>
            @if (error()) {
              <p class="message error" role="alert">{{ error() }}</p>
            }
            @if (notice()) {
              <p class="message" role="status">{{ notice() }}</p>
            }
            <div class="actions">
              <button matButton type="button" (click)="resetPassword()" [disabled]="busy()">Olvidé mi contraseña</button>
              <button matButton="filled" type="submit" [disabled]="busy()">Entrar</button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </main>
  `,
})
export class SignIn {
  private readonly auth = inject(PASSWORD_AUTH);
  private readonly router = inject(Router);

  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  async signIn(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    await this.run(async () => {
      await this.auth.signInWithPassword(this.form.controls.email.value, this.form.controls.password.value);
      await this.router.navigateByUrl('/');
    });
  }

  async resetPassword(): Promise<void> {
    const email = this.form.controls.email;
    email.markAsTouched();
    if (email.invalid) {
      this.error.set('Escribe tu correo para enviarte el enlace.');
      return;
    }
    await this.run(async () => {
      await this.auth.sendPasswordReset(email.value);
      this.notice.set('Te enviamos un enlace para crear una contraseña nueva.');
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await work();
    } catch (e) {
      this.error.set(e instanceof SignInError ? e.message : 'No se pudo iniciar sesión. Inténtalo de nuevo.');
    } finally {
      this.busy.set(false);
    }
  }
}
