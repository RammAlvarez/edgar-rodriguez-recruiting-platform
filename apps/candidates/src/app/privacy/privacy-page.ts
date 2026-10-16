import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardActions, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatCheckbox } from '@angular/material/checkbox';
import { Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { Session } from '@platform/ui/session';
import { CandidatePortal } from '../data/portal';

/**
 * First visit: the aviso de privacidad, accepted once (stamped on the person). The text is the platform's summary until the
 * owner provides the official aviso (PLAN P8).
 */
@Component({
  selector: 'app-privacy-page',
  imports: [MatCard, MatCardHeader, MatCardTitle, MatCardContent, MatCardActions, MatCheckbox, MatButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="privacy">
      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>Aviso de privacidad</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <p>Hola {{ session.me()?.displayName }}. Antes de continuar, lee cómo usamos tus datos.</p>
          <ul>
            <li>Tus datos personales y los documentos que subas se usan para tu proceso de reclutamiento y, si te contratan, para tu alta y el pago de tu nómina.</li>
            <li>Los consulta el equipo de reclutamiento. La empresa de la vacante ve tus iniciales y en qué etapa vas; cuando te seleccionamos para presentarte, ve también tu nombre, el sueldo ofrecido, la fecha de tu entrevista y cuántos documentos entregaste. Nunca ve tus documentos ni tus datos de contacto.</li>
            <li>Tus documentos se guardan en un almacenamiento privado y cada vez que alguien del equipo los consulta queda registrado.</li>
            <li>Puedes pedir que se corrijan o eliminen tus datos con tu reclutador.</li>
          </ul>
          <mat-checkbox [checked]="agreed()" (change)="agreed.set($event.checked)">He leído el aviso de privacidad y acepto el uso de mis datos</mat-checkbox>
          @if (error()) {
            <p class="form-alert" role="alert">{{ error() }}</p>
          }
        </mat-card-content>
        <mat-card-actions align="end">
          <button matButton type="button" (click)="signOut()">Salir</button>
          <button matButton="filled" type="button" [disabled]="!agreed() || busy()" (click)="accept()">Continuar</button>
        </mat-card-actions>
      </mat-card>
    </main>
  `,
  styles: `
    .privacy {
      display: grid;
      place-items: start center;
      min-height: 100%;
      padding: 16px;
      box-sizing: border-box;
      background: var(--mat-sys-surface-container-low);
    }
    mat-card {
      width: min(560px, 100%);
    }
    ul {
      padding-left: 20px;
      li {
        margin-bottom: 8px;
      }
    }
  `,
})
export class PrivacyPage {
  readonly session = inject(Session);
  private readonly portal = inject(CandidatePortal);
  private readonly router = inject(Router);
  readonly agreed = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async accept(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.portal.acceptPrivacyNotice();
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(e instanceof ApiError ? e.message : 'No se pudo guardar. Inténtalo de nuevo.');
    } finally {
      this.busy.set(false);
    }
  }

  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }
}
