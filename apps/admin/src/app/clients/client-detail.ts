import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTab, MatTabGroup } from '@angular/material/tabs';
import { MatTooltip } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { confirm, openSidePanel } from '@platform/ui';
import type { ContactDto, OpeningDetailDto, OpeningListItemDto, SiteDto } from '../data/api-types';
import { OPENING_STATUS } from '../data/recruiting-labels';
import { OpeningsRepository } from '../data/recruiting-repositories';
import { OpeningPanel, type OpeningPanelData } from '../openings/opening-panel';
import { CatalogsRepository, ClientsRepository, UsersRepository } from '../data/repositories';
import { ClientPanel, type ClientPanelData } from './client-panel';
import { ContactPanel, type ContactPanelData } from './contact-panel';
import { SitePanel, type SitePanelData } from './site-panel';

/** Reclutamiento → Clientes → detail (SPEC §4.5): counters, Vacantes with «Nueva vacante» (Cliente preset), Contactos with «Invitar al portal», Sitios. */
@Component({
  selector: 'app-client-detail',
  imports: [MatTabGroup, MatTab, MatButton, MatIconButton, MatIcon, MatProgressBar, MatTooltip, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a matButton routerLink="/clientes" class="back"><mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>Clientes</a>
      @if (client.loading() && !client.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (client.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }
      @if (client.value(); as c) {
        <header class="page-header">
          <h1>
            {{ c.shortName }}
            <span class="subtitle">{{ c.legalName ?? '' }}@if (c.rfc) { · <span class="mono">{{ c.rfc }}</span> }</span>
          </h1>
          <button matButton="outlined" type="button" (click)="edit()">
            <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>Editar
          </button>
        </header>
        <div class="kpis">
          <div class="kpi"><div class="label">Vacantes abiertas</div><div class="value">{{ openCount() }}</div></div>
          <div class="kpi"><div class="label">Cubiertas</div><div class="value">{{ filledCount() }}</div></div>
          <div class="kpi"><div class="label">Contactos</div><div class="value">{{ c.contacts.length }}</div></div>
          <div class="kpi"><div class="label">Sitios</div><div class="value">{{ c.sites.length }}</div></div>
        </div>
        <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
          <mat-tab label="Vacantes">
            <div class="tab-actions">
              <button matButton="filled" type="button" (click)="newOpening()">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>Nueva vacante
              </button>
            </div>
            <div class="list-card">
              @for (o of openings.value() ?? []; track o.id) {
                <div class="list-row clickable" tabindex="0" (click)="openOpening(o.id)" (keydown.enter)="openOpening(o.id)">
                  <mat-icon fontSet="material-symbols-outlined" class="muted">work</mat-icon>
                  <div class="grow">
                    <div class="title">
                      {{ o.displayName }}
                      <span class="pill" [class]="statusOf(o).tone">{{ statusOf(o).label }}</span>
                    </div>
                    <div class="muted">{{ o.hired }} de {{ o.headcountRequested }} contratados · {{ o.daysOpen }} días · {{ o.leadRecruiterName ?? 'Sin líder' }}</div>
                  </div>
                </div>
              } @empty {
                <p class="list-empty">Sin vacantes. Las vacantes de este cliente aparecerán aquí.</p>
              }
            </div>
          </mat-tab>
          <mat-tab label="Contactos">
            <div class="tab-actions">
              <button matButton="filled" type="button" (click)="contact(null)">
                <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Nuevo contacto
              </button>
            </div>
            <div class="list-card">
              @for (k of c.contacts; track k.id) {
                <div class="list-row">
                  <div class="grow">
                    <div class="title">
                      {{ k.fullName }}
                      @if (k.isPrimary) {
                        <span class="pill positive">Principal</span>
                      }
                    </div>
                    <div class="muted">{{ contactLine(k) }}</div>
                  </div>
                  @if (k.userId) {
                    <span class="pill positive"><mat-icon fontSet="material-symbols-outlined">verified</mat-icon>Con acceso al portal</span>
                  } @else if (k.email) {
                    <button matButton="outlined" type="button" (click)="invite(k)">
                      <mat-icon fontSet="material-symbols-outlined">send</mat-icon>Invitar al portal
                    </button>
                  }
                  <button matIconButton type="button" aria-label="Editar contacto" matTooltip="Editar contacto" (click)="contact(k)">
                    <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                  </button>
                  @if (!k.userId) {
                    <button matIconButton type="button" aria-label="Eliminar contacto" matTooltip="Eliminar contacto" (click)="remove(k)">
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  }
                </div>
              } @empty {
                <p class="list-empty">Sin contactos. Los contactos del cliente aparecerán aquí.</p>
              }
            </div>
          </mat-tab>
          <mat-tab label="Sitios">
            <div class="tab-actions">
              <button matButton="filled" type="button" (click)="site(null)">
                <mat-icon fontSet="material-symbols-outlined">add_location_alt</mat-icon>Nuevo sitio
              </button>
            </div>
            <div class="list-card">
              @for (s of c.sites; track s.id) {
                <div class="list-row clickable" tabindex="0" (click)="site(s)" (keydown.enter)="site(s)">
                  <mat-icon fontSet="material-symbols-outlined" class="muted">location_on</mat-icon>
                  <div class="grow">
                    <div class="title">
                      {{ s.name }}
                      @if (!s.isActive) {
                        <span class="pill">Inactivo</span>
                      }
                    </div>
                    <div class="muted">{{ siteLine(s) }}</div>
                  </div>
                </div>
              } @empty {
                <p class="list-empty">Sin sitios registrados.</p>
              }
            </div>
          </mat-tab>
        </mat-tab-group>
      }
    </div>
  `,
})
export class ClientDetail {
  /** Route param, read from the snapshot: the live query starts in the field initializer, before inputs are bound. */
  private readonly clientId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly id = () => this.clientId;
  private readonly repo = inject(ClientsRepository);
  private readonly users = inject(UsersRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly client = this.repo.detail('recruiting', () => this.id());
  private readonly catalogs = inject(CatalogsRepository).catalogs();
  readonly zones = computed(() => this.catalogs.value()?.zones ?? []);
  private readonly router = inject(Router);
  readonly openings = inject(OpeningsRepository).list(() => this.id());
  readonly openCount = computed(() => (this.openings.value() ?? []).filter((o) => o.status === 'open' || o.status === 'in_progress').length);
  readonly filledCount = computed(() => (this.openings.value() ?? []).filter((o) => o.status === 'filled').length);

  statusOf(o: OpeningListItemDto) {
    return OPENING_STATUS[o.status];
  }

  openOpening(id: string): void {
    void this.router.navigate(['/vacantes', id]);
  }

  /** SPEC §4.5: the same drawer as Vacantes with Cliente preset and locked. */
  newOpening(): void {
    openSidePanel<OpeningPanel, OpeningPanelData, OpeningDetailDto>(this.dialog, OpeningPanel, { clientId: this.id() })
      .afterClosed()
      .subscribe((created) => {
        if (created) this.openOpening(created.id);
      });
  }

  contactLine(k: ContactDto): string {
    return [k.jobTitle, k.email, k.phone].filter(Boolean).join(' · ') || 'Sin datos de contacto';
  }

  siteLine(s: SiteDto): string {
    const address = [s.street && `${s.street} ${s.extNumber ?? ''}`.trim(), s.neighborhood, s.postalCode && `CP ${s.postalCode}`, s.municipality]
      .filter(Boolean)
      .join(', ');
    return [s.zoneName, address].filter(Boolean).join(' · ') || 'Sin dirección';
  }

  edit(): void {
    const c = this.client.value();
    if (c) openSidePanel<ClientPanel, ClientPanelData>(this.dialog, ClientPanel, { module: 'recruiting', client: c });
  }

  contact(contact: ContactDto | null): void {
    openSidePanel<ContactPanel, ContactPanelData>(this.dialog, ContactPanel, { clientId: this.id(), contact });
  }

  site(site: SiteDto | null): void {
    openSidePanel<SitePanel, SitePanelData>(this.dialog, SitePanel, { clientId: this.id(), site, zones: this.zones() });
  }

  async remove(k: ContactDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: '¿Eliminar contacto?',
      body: `Se eliminará a ${k.fullName} de los contactos de este cliente.`,
      confirm: 'Eliminar',
      destructive: true,
    });
    if (ok) await this.attempt(() => this.repo.deleteContact(this.id(), k.id), 'Contacto eliminado');
  }

  /** «Invitar al portal»: a Cliente user linked to this client and contact; the email carries the Clients app link. */
  async invite(k: ContactDto): Promise<void> {
    const ok = await confirm(this.dialog, {
      title: '¿Invitar al portal?',
      body: `${k.fullName} recibirá en ${k.email} el acceso al portal de clientes, solo con las vacantes de este cliente.`,
      confirm: 'Enviar invitación',
    });
    if (!ok) return;
    await this.attempt(
      () =>
        this.users.invite({
          email: k.email!,
          displayName: k.fullName,
          role: 'client',
          clientId: this.id(),
          grants: null,
          clientContactId: k.id,
        }),
      `Invitación enviada a ${k.email}`,
    );
  }

  private async attempt(work: () => Promise<unknown>, done: string): Promise<void> {
    try {
      await work();
      this.snack.open(done, undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? e.message : 'Ocurrió un error inesperado.', 'Cerrar', { duration: 8000 });
    }
  }
}
