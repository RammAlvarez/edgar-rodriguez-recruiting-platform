import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { ActivatedRoute } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { TimestampPipe } from '@platform/ui/pipes';
import { ClientPortal } from '../data/portal';

/** «Comentarios» of a vacante (SPEC §4.6): the thread between the client and the recruiters; only messages meant for the client. */
@Component({
  selector: 'app-comment-thread',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatInput, MatButton, TimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="thread" aria-live="polite">
      @for (c of comments.value() ?? []; track c.id) {
        <div class="message" [class.mine]="c.fromClient">
          <div class="meta">{{ c.author }} · {{ c.createdAt | timestamp }}</div>
          <div class="body">{{ c.body }}</div>
        </div>
      } @empty {
        @if (comments.value()) {
          <p class="muted">Aún no hay comentarios. Escribe a tu reclutador aquí.</p>
        }
      }
    </div>
    <form [formGroup]="form" (ngSubmit)="send()" novalidate>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Escribe un comentario</mat-label>
        <textarea matInput formControlName="body" rows="2" maxlength="2000"></textarea>
      </mat-form-field>
      @if (error()) {
        <p class="form-alert" role="alert">{{ error() }}</p>
      }
      <button matButton="filled" type="submit" [disabled]="busy() || form.invalid">Enviar</button>
    </form>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .thread {
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-height: 420px;
      overflow-y: auto;
    }
    .message {
      align-self: flex-start;
      max-width: 85%;
      padding: 8px 12px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
      &.mine {
        align-self: flex-end;
        background: var(--mat-sys-primary-container);
        color: var(--mat-sys-on-primary-container);
      }
      .meta {
        font: var(--mat-sys-label-small);
        opacity: 0.8;
      }
      .body {
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      button {
        align-self: flex-end;
      }
    }
  `,
})
export class CommentThread {
  private readonly portal = inject(ClientPortal);
  /** From the route (the vacante detail), read in the field initializer: the live query starts before inputs are bound. */
  private readonly openingId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly comments = this.portal.comments(() => this.openingId);
  readonly form = inject(FormBuilder).nonNullable.group({ body: ['', [Validators.required, Validators.pattern(/\S/)]] });
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async send(): Promise<void> {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.portal.post(this.openingId, this.form.controls.body.value.trim());
      this.form.reset();
    } catch (e) {
      this.error.set(e instanceof ApiError ? e.message : 'No se pudo enviar. Inténtalo de nuevo.');
    } finally {
      this.busy.set(false);
    }
  }
}
