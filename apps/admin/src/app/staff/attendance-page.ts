import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiError } from '@platform/api-client';
import { addDaysToKey, type DateKey, formatDateKey, isDateKey, todayKey } from '@platform/shared';
import { openSidePanel } from '@platform/ui';
import type { AttendanceEntryDto, AttendanceStatus } from '../data/api-types';
import { ATTENDANCE } from '../data/payroll-labels';
import { AttendanceRepository } from '../data/payroll-repositories';
import { CAPTURE_STATUSES, CHIP, QUICK_STATUSES, WORKED } from './attendance-labels';
import { UnlockPanel, type UnlockPanelData } from './unlock-panel';

interface Draft {
  readonly status: AttendanceStatus;
  readonly hours: string;
  readonly overtime: string;
}

const DAYS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];

/**
 * Nómina → Asistencia (SPEC §5.3): the supervisor's daily capture, mobile-first — a date, a site, and the roster of who is
 * expected there with one-tap status and hours; «Todos presentes»; «Guardar». A closed day is read-only; an Administrador with
 * Asistencia for every client changes it with a reason (PLAN D83).
 */
@Component({
  selector: 'app-attendance-page',
  imports: [MatButton, MatIconButton, MatFormField, MatLabel, MatIcon, MatInput, MatMenu, MatMenuItem, MatMenuTrigger, MatProgressBar, MatSelect, MatOption],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      <header class="page-header"><h1>Asistencia</h1></header>
      <div class="when">
        <button matIconButton type="button" (click)="setDate(addDays(date(), -1))" aria-label="Día anterior">
          <mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon>
        </button>
        <mat-form-field subscriptSizing="dynamic" class="date">
          <mat-label>{{ weekday() }}</mat-label>
          <input matInput type="date" [value]="date()" [max]="today" (change)="pickDate($event)" />
        </mat-form-field>
        <button matIconButton type="button" (click)="setDate(addDays(date(), 1))" [disabled]="date() >= today" aria-label="Día siguiente">
          <mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon>
        </button>
        @if (date() !== today) {
          <button matButton type="button" (click)="setDate(today)">Hoy</button>
        }
      </div>
      <mat-form-field subscriptSizing="dynamic" class="site">
        <mat-label>Sitio</mat-label>
        <mat-select [value]="siteId()" (valueChange)="setSite($event)">
          @for (s of sites.value() ?? []; track s.siteId) {
            <mat-option [value]="s.siteId">{{ s.siteName }} · {{ s.captured }}/{{ s.expected }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (sites.value()?.length === 0) {
        <p class="list-empty">Nadie programado este día en tus sitios.</p>
      }
      @if (day.loading() && !day.value()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (day.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }

      @if (day.value(); as d) {
        @if (d.holiday) {
          <p class="banner"><mat-icon fontSet="material-symbols-outlined">celebration</mat-icon>{{ d.holiday }}: quien no trabaja es Festivo; quien trabaja, Festivo laborado.</p>
        }
        @if (closed()) {
          <p class="banner warn">
            <mat-icon fontSet="material-symbols-outlined">lock</mat-icon>
            Este día ya cerró.@if (d.canUnlock) { Usa «Cambiar» en cada persona; el motivo queda registrado. } @else { Para corregirlo pide el cambio al administrador. }
          </p>
        } @else {
          <div class="bulk">
            <button matButton="outlined" type="button" (click)="all(d.holiday ? 'holiday_worked' : 'present')" [disabled]="!editable().length">
              <mat-icon fontSet="material-symbols-outlined">done_all</mat-icon>{{ d.holiday ? 'Todos festivo laborado' : 'Todos presentes' }}
            </button>
            @if (d.others.length) {
              <button matButton type="button" [matMenuTriggerFor]="add"><mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>Agregar</button>
              <mat-menu #add="matMenu">
                @for (o of d.others; track o.employeeId) {
                  <button mat-menu-item type="button" (click)="addOther(o.employeeId)">{{ o.fullName }} · {{ o.positionName }}</button>
                }
              </mat-menu>
            }
          </div>
        }
        <div class="roster">
          @for (e of roster(); track e.employeeId) {
            <section class="row" [class.done]="!!e.record && !changed(e)">
              <div class="who">
                <div class="name">{{ e.fullName }}</div>
                <div class="muted">
                  {{ e.positionName }}@if (e.startTime) { · {{ e.startTime.slice(0, 5) }}–{{ e.endTime?.slice(0, 5) }} } @else { · sin horario aquí }
                </div>
              </div>
              @if (e.statusPeriod) {
                <span class="pill">{{ e.statusPeriod }}</span>
              } @else if (e.locked) {
                <span class="pill" [class]="e.record ? label[e.record.status!].tone : 'negative'">
                  {{ e.record ? label[e.record.status!].label + hoursText(e.record.status!, e.record.hoursWorked) : 'Sin captura' }}
                </span>
                @if (e.laterStatus) {
                  <span class="muted later">Estatus registrado después del pago: {{ e.laterStatus }}</span>
                }
                @if (d.canUnlock) {
                  <button matButton type="button" (click)="unlock(e)">Cambiar</button>
                }
              } @else {
                <div class="chips" role="radiogroup" [attr.aria-label]="'Asistencia de ' + e.fullName">
                  @for (s of quick; track s) {
                    <button type="button" class="chip" role="radio" [class.on]="draft(e)?.status === s" [attr.aria-checked]="draft(e)?.status === s"
                      (click)="set(e, s)">{{ chip[s] }}</button>
                  }
                  <button type="button" class="chip" [class.on]="isMore(e)" [matMenuTriggerFor]="more">{{ isMore(e) ? label[draft(e)!.status].label : 'Más' }}</button>
                  <mat-menu #more="matMenu">
                    @for (s of moreStatuses; track s) {
                      <button mat-menu-item type="button" (click)="set(e, s)">{{ label[s].label }}</button>
                    }
                  </mat-menu>
                </div>
                @if (draft(e); as dr) {
                  @if (worked(dr.status)) {
                    <div class="hours">
                      <label>
                        Horas
                        <input type="number" inputmode="decimal" min="0" max="24" step="0.5" [value]="dr.hours" [placeholder]="'' + (+e.shiftHours)"
                          (input)="setHours(e, 'hours', $event)" />
                      </label>
                      <label>
                        Extra
                        <input type="number" inputmode="decimal" min="0" max="24" step="0.5" [value]="dr.overtime" placeholder="0"
                          (input)="setHours(e, 'overtime', $event)" />
                      </label>
                    </div>
                  }
                }
              }
            </section>
          } @empty {
            <p class="list-empty">Nadie programado aquí este día.</p>
          }
        </div>
        @if (!closed()) {
          <div class="save">
            <span class="muted">{{ pending().length }} por guardar</span>
            <button matButton="filled" type="button" (click)="save()" [disabled]="saving() || !pending().length">Guardar</button>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .narrow {
      max-width: 760px;
    }
    .when {
      display: flex;
      align-items: center;
      gap: 4px;
      margin-bottom: 12px;
      .date {
        width: 180px;
      }
    }
    .site {
      width: 100%;
      margin-bottom: 12px;
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
      &.warn {
        background: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
      }
    }
    .bulk {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .roster {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      &:last-child {
        border-bottom: 0;
      }
      &.done .name::after {
        content: ' ✓';
        color: var(--mat-sys-primary);
      }
    }
    .who {
      flex: 1 1 200px;
      min-width: 0;
      .name {
        font-weight: 500;
      }
    }
    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .chip {
      all: unset;
      box-sizing: border-box;
      min-height: 36px;
      padding: 6px 12px;
      border-radius: 18px;
      border: 1px solid var(--mat-sys-outline);
      font: var(--mat-sys-label-large);
      cursor: pointer;
      &.on {
        background: var(--mat-sys-secondary-container);
        color: var(--mat-sys-on-secondary-container);
        border-color: transparent;
      }
      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
      }
    }
    .hours {
      display: flex;
      gap: 12px;
      width: 100%;
      label {
        display: flex;
        align-items: center;
        gap: 6px;
        font: var(--mat-sys-label-medium);
      }
      input {
        width: 64px;
        min-height: 32px;
        padding: 0 6px;
        border: 1px solid var(--mat-sys-outline);
        border-radius: 8px;
        background: transparent;
        color: inherit;
        font: inherit;
      }
    }
    .save {
      position: sticky;
      bottom: 0;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 12px;
      padding: 12px 0;
      background: var(--mat-sys-surface);
    }
  `,
})
export class AttendancePage {
  private readonly repo = inject(AttendanceRepository);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  readonly today = todayKey();
  readonly label = ATTENDANCE;
  readonly chip = CHIP;
  readonly quick = QUICK_STATUSES;
  readonly moreStatuses = CAPTURE_STATUSES.filter((s) => !QUICK_STATUSES.includes(s));
  readonly addDays = addDaysToKey;

  readonly date = signal<DateKey>(isDateKey(this.params.get('date')) ? (this.params.get('date') as DateKey) : this.today);
  readonly siteId = signal<string | null>(this.params.get('site'));
  readonly sites = this.repo.sites(() => this.date());
  readonly day = this.repo.day(() => ({ siteId: this.siteId(), date: this.date() }));
  readonly drafts = signal(new Map<string, Draft>());
  /** Employees added from «Agregar» who have no schedule here (shown until saved). */
  readonly added = signal<string[]>([]);
  readonly saving = signal(false);

  readonly weekday = computed(() => DAYS[new Date(`${this.date()}T12:00:00Z`).getUTCDay()]);
  readonly roster = computed(() => {
    const d = this.day.value();
    if (!d) return [];
    const extra = d.others
      .filter((o) => this.added().includes(o.employeeId))
      .map((o): AttendanceEntryDto => ({
        employeeId: o.employeeId, fullName: o.fullName, positionName: o.positionName, shiftHours: d.entries[0]?.shiftHours ?? '8',
        startTime: null, endTime: null, scheduled: false, record: null, locked: false, statusPeriod: null, laterStatus: null,
      }));
    return [...d.entries, ...extra];
  });
  readonly closed = computed(() => this.roster().length > 0 && this.roster().every((e) => e.locked || e.statusPeriod));
  readonly editable = computed(() => this.roster().filter((e) => !e.locked && !e.statusPeriod));
  readonly pending = computed(() => this.editable().filter((e) => this.changed(e)));

  constructor() {
    // Date or site changed: reload both lists and drop the drafts (the URL keeps the choice for the Calendario links).
    let first = true;
    effect(() => {
      const date = this.date();
      const site = this.siteId();
      void this.router.navigate([], { queryParams: { date, site }, replaceUrl: true });
      if (first) {
        first = false;
        return;
      }
      untracked(() => {
        this.drafts.set(new Map());
        this.added.set([]);
        this.sites.reload();
        this.day.reload();
      });
    });
    // The first site of the date when none is chosen (or the chosen one has nobody that day).
    effect(() => {
      const sites = this.sites.value();
      if (sites?.length && !sites.some((s) => s.siteId === untracked(this.siteId))) this.siteId.set(sites[0].siteId);
    });
  }

  draft(e: AttendanceEntryDto): Draft | undefined {
    return (
      this.drafts().get(e.employeeId) ??
      (e.record
        ? { status: e.record.status!, hours: e.record.hoursWorked == null ? '' : String(Number(e.record.hoursWorked)), overtime: Number(e.record.overtimeHours) > 0 ? String(Number(e.record.overtimeHours)) : '' }
        : undefined)
    );
  }

  changed(e: AttendanceEntryDto): boolean {
    const d = this.drafts().get(e.employeeId);
    if (!d) return false;
    const r = e.record;
    return !r || r.status! !== d.status || (r.hoursWorked == null ? '' : String(Number(r.hoursWorked))) !== d.hours
      || (Number(r.overtimeHours) > 0 ? String(Number(r.overtimeHours)) : '') !== d.overtime;
  }

  worked(s: AttendanceStatus): boolean {
    return WORKED.includes(s);
  }

  isMore(e: AttendanceEntryDto): boolean {
    const s = this.draft(e)?.status;
    return !!s && !QUICK_STATUSES.includes(s);
  }

  hoursText(status: AttendanceStatus, hours: string | null | undefined): string {
    return this.worked(status) && hours ? ` · ${Number(hours)} h` : '';
  }

  set(e: AttendanceEntryDto, status: AttendanceStatus): void {
    const current = this.draft(e);
    this.drafts.update((m) => new Map(m).set(e.employeeId, {
      status,
      hours: status === 'partial' ? (current?.status === 'partial' ? current.hours : '') : this.worked(status) ? (current?.hours ?? '') : '',
      overtime: this.worked(status) ? (current?.overtime ?? '') : '',
    }));
  }

  setHours(e: AttendanceEntryDto, field: 'hours' | 'overtime', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const current = this.draft(e) ?? { status: 'present' as AttendanceStatus, hours: '', overtime: '' };
    this.drafts.update((m) => new Map(m).set(e.employeeId, { ...current, [field]: value }));
  }

  /** «Todos presentes»: everyone not captured yet (a captured value is never overwritten). */
  all(status: AttendanceStatus): void {
    for (const e of this.editable()) if (!this.draft(e)) this.set(e, status);
  }

  addOther(employeeId: string): void {
    this.added.update((a) => [...a, employeeId]);
  }

  setDate(date: DateKey): void {
    if (date <= this.today) this.date.set(date);
  }

  pickDate(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (isDateKey(value)) this.setDate(value);
  }

  setSite(siteId: string): void {
    this.siteId.set(siteId);
  }

  async save(): Promise<void> {
    const d = this.day.value();
    if (!d) return;
    this.saving.set(true);
    try {
      const entries = this.pending().map((e) => {
        const dr = this.draft(e)!;
        return { employeeId: e.employeeId, status: dr.status, hoursWorked: dr.hours.trim() || null, overtimeHours: dr.overtime.trim() || null, note: null };
      });
      await this.repo.save(d.siteId, d.date, entries);
      this.drafts.set(new Map());
      this.added.set([]);
      this.snack.open(`Asistencia guardada · ${entries.length} ${entries.length === 1 ? 'persona' : 'personas'}`, undefined, { duration: 4000 });
    } catch (e) {
      this.snack.open(e instanceof ApiError ? (e.fieldErrors ? Object.values(e.fieldErrors)[0]?.[0] ?? e.message : e.message) : 'No se pudo guardar.', 'Cerrar');
    } finally {
      this.saving.set(false);
    }
  }

  unlock(e: AttendanceEntryDto): void {
    const d = this.day.value();
    if (!d) return;
    openSidePanel<UnlockPanel, UnlockPanelData>(this.dialog, UnlockPanel, { siteId: d.siteId, date: d.date, entry: e });
  }

  formatDate(d: DateKey): string {
    return formatDateKey(d);
  }
}
