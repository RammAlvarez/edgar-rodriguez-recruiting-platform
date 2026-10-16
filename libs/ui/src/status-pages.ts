import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Session } from './session';

/** Shown when the provider signed the user in but the API has no active users row for them (403 from /v1/me). */
@Component({
  selector: 'ui-no-access-page',
  imports: [MatButton, MatIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page empty-state">
      <mat-icon fontSet="material-symbols-outlined">lock_person</mat-icon>
      <h1>Sin acceso</h1>
      <p>{{ message() }}</p>
      <button matButton="outlined" type="button" (click)="signOut()">Cerrar sesión</button>
    </main>
  `,
})
export class NoAccessPage {
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  readonly message = computed(() => {
    const s = this.session.state();
    return s.status === 'denied' ? s.message : 'Tu cuenta no tiene acceso a esta aplicación.';
  });

  /** Back to «Entrar» (every app has it) to try another email; staying here showed «Sin acceso» again (D-010). */
  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigate(['/entrar']);
  }
}

/** Placeholder for a screen that a later slice of docs/PLAN.md delivers. */
@Component({
  selector: 'ui-coming-soon-page',
  imports: [MatIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>{{ title() }}</h1></header>
      <div class="empty-state">
        <mat-icon fontSet="material-symbols-outlined">{{ icon() }}</mat-icon>
        <p>Esta sección todavía no está disponible. Aparecerá aquí en una próxima entrega.</p>
      </div>
    </div>
  `,
})
export class ComingSoonPage {
  readonly title = input.required<string>();
  readonly icon = input('construction');
}
