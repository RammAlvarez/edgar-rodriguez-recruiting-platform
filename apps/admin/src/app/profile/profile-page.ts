import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { Session, ThemePicker } from '@platform/ui';

const ROLE_LABELS = { administrator: 'Administrador', recruiter: 'Reclutador', client: 'Cliente', candidate: 'Candidato' } as const;

/** «Mi perfil» of staff: who is signed in, the theme of this device and the way to the two-step verification. */
@Component({
  selector: 'app-profile-page',
  imports: [RouterLink, MatButton, MatIcon, ThemePicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .section {
      display: flex;
      flex-direction: column;
      gap: 12px;
      max-width: 640px;
      margin-bottom: 16px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      h2 {
        margin: 0;
        font: var(--mat-sys-title-medium);
      }
      p {
        margin: 0;
      }
    }
    dl {
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 8px 16px;
      margin: 0;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
    }
  `,
  template: `
    <div class="page">
      <header class="page-header"><h1>Mi perfil</h1></header>
      @if (me(); as m) {
        <section class="section">
          <h2>Cuenta</h2>
          <dl>
            <dt>Nombre</dt>
            <dd>{{ m.displayName }}</dd>
            <dt>Correo</dt>
            <dd>{{ m.email }}</dd>
            <dt>Rol</dt>
            <dd>{{ role() }}</dd>
          </dl>
        </section>
      }
      <section class="section">
        <h2>Apariencia</h2>
        <p class="muted">Claro es el predeterminado. Automático sigue el tema de tu dispositivo. Se guarda en este navegador.</p>
        <ui-theme-picker />
      </section>
      <section class="section">
        <h2>Seguridad</h2>
        <p class="muted">Un código de tu app de autenticación además de tu contraseña.</p>
        <div>
          <a matButton="outlined" routerLink="/verificacion">
            <mat-icon fontSet="material-symbols-outlined">shield_lock</mat-icon>
            Verificación en dos pasos
          </a>
        </div>
      </section>
    </div>
  `,
})
export class ProfilePage {
  readonly me = inject(Session).me;
  readonly role = computed(() => {
    const m = this.me();
    return m ? ROLE_LABELS[m.role] : '';
  });
}
