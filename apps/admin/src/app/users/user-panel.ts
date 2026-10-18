import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '@platform/api-client';
import { email as emailSchema, type PermissionModule, type UserRole } from '@platform/shared';
import { applyServerErrors, confirm, errorMessage, zodValidator } from '@platform/ui';
import type { ClientOptionDto, UserDto } from '../data/api-types';
import { DEFAULT_ADMIN_GRANTS, GRANTS, ROLE_LABELS } from '../data/labels';
import { MfaRepository } from '../data/mfa-repository';
import { UsersRepository } from '../data/repositories';
import { accessSummary, fromGrants, toGrants } from './grants';

export interface UserPanelData {
  /** null = «Invitar usuario». */
  readonly user: UserDto | null;
  readonly clients: readonly ClientOptionDto[];
  readonly myId: string;
}

/** «Invitar usuario» / «Editar usuario»: one side panel with the same fields (ARCHITECTURE §9.12). */
@Component({
  selector: 'app-user-panel',
  imports: [
    ReactiveFormsModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatInput,
    MatSelect,
    MatOption,
    MatCheckbox,
    MatSlideToggle,
    MatButton,
    MatIcon,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ editing ? 'Editar usuario' : 'Invitar usuario' }}</h2>
    <mat-dialog-content>
      <form class="panel-form" [formGroup]="form" (ngSubmit)="save()" id="user-form" novalidate>
        <mat-form-field>
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="displayName" autocomplete="off" />
          <mat-error>{{ msg('displayName') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Correo</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="off" [readonly]="emailLocked" />
          @if (emailLocked) {
            <mat-hint>Invitación aceptada · el correo no se puede cambiar</mat-hint>
          }
          <mat-error>{{ msg('email') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Rol</mat-label>
          <mat-select formControlName="role">
            @for (r of roles; track r.value) {
              <mat-option [value]="r.value">{{ r.label }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ msg('role') }}</mat-error>
        </mat-form-field>
        @if (role() === 'client') {
          <mat-form-field>
            <mat-label>Cliente</mat-label>
            <mat-select formControlName="clientId">
              @for (c of data.clients; track c.id) {
                <mat-option [value]="c.id">{{ c.shortName }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ msg('clientId') }}</mat-error>
          </mat-form-field>
        }
        @if (editing && summary) {
          <p class="access"><strong>Acceso:</strong> {{ summary }}</p>
        }
        @if (editing) {
          <mat-slide-toggle formControlName="isActive">Activo</mat-slide-toggle>
          <mat-slide-toggle formControlName="mfaRequired">Requerir verificación en dos pasos</mat-slide-toggle>
        }
        @if (role() === 'administrator') {
          <h3 class="section-title">Permisos delegados</h3>
          <div formGroupName="grants" class="checks">
            @for (g of grantOptions; track g.module) {
              <mat-checkbox [formControlName]="g.module">{{ g.label }}</mat-checkbox>
            }
          </div>
          <mat-form-field>
            <mat-label>Solo para el cliente (opcional)</mat-label>
            <mat-select formControlName="onlyClientId">
              <mat-option [value]="null">Todos los clientes</mat-option>
              @for (c of data.clients; track c.id) {
                <mat-option [value]="c.id">{{ c.shortName }}</mat-option>
              }
            </mat-select>
            <mat-hint>{{ scopeHint }}</mat-hint>
            <mat-error>{{ msg('onlyClientId') }}</mat-error>
          </mat-form-field>
          @if (form.controls.grants.errors?.['server']; as m) {
            <p class="form-alert">{{ m }}</p>
          }
        }
        @if (alert()) {
          <p class="form-alert" role="alert">{{ alert() }}</p>
        }
        @if (editing) {
          <div class="secondary-actions">
            <button matButton="outlined" type="button" (click)="resend()" [disabled]="busy() || !data.user!.isActive">
              <mat-icon fontSet="material-symbols-outlined">forward_to_inbox</mat-icon>Reenviar invitación
            </button>
            @if (data.user!.id !== data.myId && data.user!.role !== 'client') {
              <button matButton="outlined" type="button" (click)="resetMfa()" [disabled]="busy()">Restablecer verificación en dos pasos</button>
            }
            @if (data.user!.isActive && data.user!.id !== data.myId) {
              <button matButton="outlined" class="destructive" type="button" (click)="deactivate()" [disabled]="busy()">
                <mat-icon fontSet="material-symbols-outlined">person_off</mat-icon>Desactivar usuario
              </button>
            }
          </div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="ref.close()">Cancelar</button>
      <button matButton="filled" type="submit" form="user-form" [disabled]="busy()">{{ editing ? 'Guardar' : 'Enviar invitación' }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .checks {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    }
    mat-slide-toggle {
      margin: 4px 0 12px;
    }
    .access {
      margin: 0 0 8px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class UserPanel {
  readonly data = inject<UserPanelData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<UserPanel>);
  private readonly users = inject(UsersRepository);
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly mfa = inject(MfaRepository);

  readonly editing = this.data.user !== null;
  /** What the user can reach and over which clients, as saved (the checkboxes below edit it). */
  readonly summary = this.data.user
    ? accessSummary(this.data.user, (id) => this.data.clients.find((c) => c.id === id)?.shortName ?? '—')
    : null;
  readonly emailLocked = !!this.data.user?.acceptedAt;
  readonly roles = (Object.keys(ROLE_LABELS) as UserRole[]).map((value) => ({ value, label: ROLE_LABELS[value] }));
  readonly grantOptions = GRANTS;
  private readonly initial = fromGrants(this.data.user?.grants ?? []);
  readonly scopeHint = this.initial.mixed
    ? 'Los permisos tenían distintos clientes; al guardar se aplica el cliente elegido.'
    : 'Reclutamiento, Usuarios y Reportes de reclutamiento aplican siempre a todos los clientes.';

  readonly form = inject(FormBuilder).group({
    displayName: [this.data.user?.displayName ?? '', [Validators.required, Validators.maxLength(120)]],
    email: [this.data.user?.email ?? '', [Validators.required, zodValidator(emailSchema)]],
    role: [(this.data.user?.role ?? 'recruiter') as UserRole, Validators.required],
    clientId: [this.data.user?.clientId ?? (null as string | null)],
    isActive: [this.data.user?.isActive ?? true],
    mfaRequired: [this.data.user?.mfaRequired ?? false],
    grants: inject(FormBuilder).group(
      Object.fromEntries(
        GRANTS.map((g) => [
          g.module,
          this.data.user ? this.initial.modules.includes(g.module) : DEFAULT_ADMIN_GRANTS.includes(g.module),
        ]),
      ) as Record<PermissionModule, boolean>,
    ),
    onlyClientId: [this.initial.onlyClientId],
  });

  readonly role = toSignal(this.form.controls.role.valueChanges, { initialValue: this.form.controls.role.value });
  readonly busy = signal(false);
  readonly alert = signal<string | null>(null);

  msg(field: string): string | null {
    return errorMessage(this.form.get(field));
  }

  async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const role = v.role!;
    const modules = GRANTS.map((g) => g.module).filter((m) => v.grants[m]);
    const grants = role === 'administrator' ? toGrants(modules, v.onlyClientId ?? null) : null;
    const clientId = role === 'client' ? (v.clientId ?? null) : null;
    await this.run(async () => {
      if (this.data.user) {
        await this.users.update(this.data.user.id, {
          email: v.email!.trim(),
          displayName: v.displayName!.trim(),
          role,
          clientId,
          isActive: v.isActive!,
          mfaRequired: v.mfaRequired!,
          grants,
        });
        this.snack.open('Usuario actualizado', undefined, { duration: 4000 });
      } else {
        const result = await this.users.invite({
          email: v.email!.trim(),
          displayName: v.displayName!.trim(),
          role,
          clientId,
          grants,
          clientContactId: null,
        });
        this.snack.open(
          result.invitationSent ? `Invitación enviada a ${result.user.email}` : 'Usuario creado; no se pudo enviar el correo',
          undefined,
          { duration: 5000 },
        );
      }
      this.ref.close(true);
    });
  }

  async resend(): Promise<void> {
    await this.run(async () => {
      const r = await this.users.resendInvitation(this.data.user!.id);
      this.snack.open(r.invitationSent ? `Invitación reenviada a ${r.user.email}` : 'No se pudo enviar el correo', undefined, {
        duration: 4000,
      });
    });
  }

  /** A lost phone (PLAN D89): their next sign-in sets it up again if their account requires it. */
  async resetMfa(): Promise<void> {
    const u = this.data.user!;
    const ok = await confirm(this.dialog, {
      title: '¿Restablecer la verificación en dos pasos?',
      body: `${u.displayName} tendrá que volver a activarla con su app al iniciar sesión (si su cuenta la requiere). Sus códigos de recuperación dejan de servir.`,
      confirm: 'Restablecer',
    });
    if (!ok) return;
    await this.run(async () => {
      await this.mfa.reset(u.id);
      this.snack.open('Verificación en dos pasos restablecida', undefined, { duration: 4000 });
    });
  }

  async deactivate(): Promise<void> {
    const u = this.data.user!;
    const ok = await confirm(this.dialog, {
      title: '¿Desactivar usuario?',
      body: `${u.displayName} ya no podrá entrar a la plataforma. Puedes volver a activarlo después.`,
      confirm: 'Desactivar',
      destructive: true,
    });
    if (!ok) return;
    this.form.controls.isActive.setValue(false);
    await this.save();
  }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.alert.set(null);
    try {
      await work();
    } catch (e) {
      if (e instanceof ApiError && e.isValidation) {
        const rest = applyServerErrors(this.form, e);
        this.alert.set(rest[0] ?? (Object.keys(e.fieldErrors).length ? null : e.message));
      } else {
        this.alert.set(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.');
      }
    } finally {
      this.busy.set(false);
    }
  }
}
