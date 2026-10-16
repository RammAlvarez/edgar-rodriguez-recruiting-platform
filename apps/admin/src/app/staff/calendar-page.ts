import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { addDaysToKey, type DateKey, formatDateKey, todayKey } from '@platform/shared';
import type { AttendanceStatus, CalendarDayDto } from '../data/api-types';
import { ATTENDANCE } from '../data/payroll-labels';
import { CalendarRepository } from '../data/payroll-repositories';

type View = 'day' | 'week' | 'month';

/** Client colours (design system tonal palette), assigned in order of appearance. */
const PALETTE = ['#1565c0', '#2e7d32', '#ad1457', '#ef6c00', '#6a1b9a', '#00838f', '#5d4037', '#455a64'];
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
const DAYS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

/** ISO weekday of a date key: 1 = lunes … 7 = domingo. */
function isoWeekday(d: DateKey): number {
  const day = new Date(`${d}T12:00:00Z`).getUTCDay();
  return day === 0 ? 7 : day;
}

/**
 * Nómina → Calendario de servicios (SPEC §5.2): which jobs are scheduled per day and who is expected, by month, week or day,
 * coloured by client. «Hoy» lists each site with its expected staff and the attendance already recorded (vacaciones,
 * incapacidad…); in S8 the day view becomes the entry to attendance capture.
 */
@Component({
  selector: 'app-calendar-page',
  imports: [RouterLink, MatButton, MatIconButton, MatButtonToggleGroup, MatButtonToggle, MatFormField, MatLabel, MatIcon, MatProgressBar, MatSelect, MatOption],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><h1>Calendario de servicios</h1></header>
      <div class="filter-bar">
        <mat-button-toggle-group [value]="view()" (change)="view.set($event.value)" aria-label="Vista" hideSingleSelectionIndicator>
          <mat-button-toggle value="day">Día</mat-button-toggle>
          <mat-button-toggle value="week">Semana</mat-button-toggle>
          <mat-button-toggle value="month">Mes</mat-button-toggle>
        </mat-button-toggle-group>
        <span class="nav">
          <button matIconButton type="button" (click)="move(-1)" aria-label="Anterior"><mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon></button>
          <button matButton type="button" (click)="anchor.set(today)">Hoy</button>
          <button matIconButton type="button" (click)="move(1)" aria-label="Siguiente"><mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon></button>
        </span>
        <strong class="label">{{ label() }}</strong>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Cliente</mat-label>
          <mat-select [value]="clientId()" (valueChange)="clientId.set($event)">
            <mat-option [value]="null">Todos</mat-option>
            @for (c of clients(); track c.id) {
              <mat-option [value]="c.id">{{ c.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      @if (calendar.loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (calendar.error(); as e) {
        <p class="form-alert" role="alert">{{ e.message }}</p>
      }

      @switch (view()) {
        @case ('day') {
          @if (dayOf(anchor()); as d) {
            @for (s of d.sites; track s.siteId) {
              <section class="site" [style.border-left-color]="color(s.clientId)">
                <h2>{{ s.siteName }} <span class="muted">· {{ s.entries.length }} esperados</span></h2>
                <div class="list-card">
                  @for (x of s.entries; track x.employeeId) {
                    <a class="list-row clickable" [routerLink]="['/personal', x.employeeId]">
                      <span class="time">{{ x.startTime.slice(0, 5) }}–{{ x.endTime.slice(0, 5) }}</span>
                      <span class="grow">
                        <span class="who">{{ x.fullName }}</span>
                        <span class="muted"> · {{ x.positionName }}@if (x.jobDescription) { · {{ x.jobDescription }} }</span>
                      </span>
                      @if (x.attendance; as a) {
                        <span class="pill" [class]="attendance[a].tone">{{ attendance[a].label }}</span>
                      } @else {
                        <span class="pill">Sin capturar</span>
                      }
                    </a>
                  }
                </div>
              </section>
            } @empty {
              @if (calendar.value()) {
                <p class="list-empty">Nadie programado este día.</p>
              }
            }
          }
        }
        @case ('week') {
          <div class="week">
            @for (d of days(); track d.date) {
              <div class="col" [class.today]="d.date === today">
                <button type="button" class="col-head" (click)="anchor.set(d.date); view.set('day')">
                  {{ dayName(d.date) }} <strong>{{ d.date.slice(8) }}</strong>
                </button>
                @for (s of d.sites; track s.siteId) {
                  <div class="chip" [style.border-left-color]="color(s.clientId)">
                    <div class="chip-title">{{ s.siteName }}</div>
                    @for (x of s.entries; track x.employeeId) {
                      <div class="chip-line" [class.away]="isAway(x.attendance)">{{ x.startTime.slice(0, 5) }} {{ short(x.fullName) }}</div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        }
        @case ('month') {
          <div class="month">
            @for (n of dayNames; track n) {
              <div class="month-head">{{ n }}</div>
            }
            @for (blank of leading(); track $index) {
              <div class="cell empty"></div>
            }
            @for (d of days(); track d.date) {
              <button type="button" class="cell" [class.today]="d.date === today" (click)="anchor.set(d.date); view.set('day')">
                <span class="num">{{ +d.date.slice(8) }}</span>
                @for (c of perClient(d); track c.clientId) {
                  <span class="dot" [style.background]="color(c.clientId)">{{ c.count }}</span>
                }
              </button>
            }
          </div>
          <div class="legend">
            @for (c of clients(); track c.id) {
              <span><span class="swatch" [style.background]="color(c.id)"></span>{{ c.name }}</span>
            }
          </div>
        }
      }
    </div>
  `,
  styles: `
    .nav {
      display: inline-flex;
      align-items: center;
    }
    .label {
      font: var(--mat-sys-title-medium);
      text-transform: capitalize;
    }
    .site {
      margin-bottom: 16px;
      padding-left: 12px;
      border-left: 4px solid;
      h2 {
        margin: 0 0 8px;
        font: var(--mat-sys-title-small);
      }
    }
    a.list-row {
      color: inherit;
      text-decoration: none;
      flex-wrap: nowrap;
    }
    .time {
      font-variant-numeric: tabular-nums;
      width: 92px;
      flex-shrink: 0;
    }
    .who {
      font-weight: 500;
    }
    .week {
      display: grid;
      grid-template-columns: repeat(7, minmax(120px, 1fr));
      gap: 8px;
      overflow-x: auto;
    }
    .col {
      min-width: 0;
      &.today .col-head {
        background: var(--mat-sys-primary-container);
        color: var(--mat-sys-on-primary-container);
      }
    }
    .col-head {
      all: unset;
      box-sizing: border-box;
      display: block;
      width: 100%;
      padding: 6px 8px;
      border-radius: 8px;
      cursor: pointer;
      text-align: center;
      background: var(--mat-sys-surface-container);
      margin-bottom: 6px;
    }
    .chip {
      margin-bottom: 6px;
      padding: 6px 8px;
      border-radius: 8px;
      border-left: 4px solid;
      background: var(--mat-sys-surface-container-low);
      font: var(--mat-sys-body-small);
    }
    .chip-title {
      font-weight: 500;
      margin-bottom: 2px;
    }
    .chip-line.away {
      text-decoration: line-through;
      color: var(--mat-sys-on-surface-variant);
    }
    .month {
      display: grid;
      grid-template-columns: repeat(7, minmax(0, 1fr));
      gap: 4px;
    }
    .month-head {
      text-align: center;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .cell {
      all: unset;
      box-sizing: border-box;
      min-height: 72px;
      padding: 4px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      cursor: pointer;
      display: flex;
      flex-wrap: wrap;
      align-content: flex-start;
      gap: 3px;
      &.empty {
        border: 0;
        cursor: default;
      }
      &.today {
        border-color: var(--mat-sys-primary);
        border-width: 2px;
      }
      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
      }
      .num {
        width: 100%;
        font: var(--mat-sys-label-medium);
      }
    }
    .dot {
      min-width: 20px;
      padding: 0 4px;
      border-radius: 10px;
      color: #fff;
      font: var(--mat-sys-label-small);
      text-align: center;
      line-height: 18px;
    }
    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-top: 12px;
      font: var(--mat-sys-body-small);
    }
    .swatch {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      margin-right: 4px;
    }
  `,
})
export class CalendarPage {
  readonly today = todayKey();
  readonly view = signal<View>('day');
  readonly anchor = signal<DateKey>(this.today);
  readonly clientId = signal<string | null>(null);
  readonly attendance = ATTENDANCE;
  readonly dayNames = DAYS;

  /** The dates the view shows: one day, the Monday–Sunday week, or the whole month. */
  readonly range = computed(() => {
    const a = this.anchor();
    if (this.view() === 'day') return { from: a, to: a };
    if (this.view() === 'week') {
      const monday = addDaysToKey(a, 1 - isoWeekday(a));
      return { from: monday, to: addDaysToKey(monday, 6) };
    }
    const first = `${a.slice(0, 8)}01` as DateKey;
    const next = addDaysToKey(first, 31).slice(0, 8);
    return { from: first, to: addDaysToKey(`${next}01` as DateKey, -1) };
  });

  private readonly repo = inject(CalendarRepository);
  readonly calendar = this.repo.range(() => ({ ...this.range(), clientId: this.clientId() }));
  readonly days = computed(() => this.calendar.value() ?? []);

  private readonly colors = new Map<string, string>();
  private readonly known = signal(new Map<string, string>());
  readonly clients = computed(() => [...this.known()].map(([id, name]) => ({ id, name })));

  constructor() {
    // Range and client change what the API returns.
    let first = true;
    effect(() => {
      this.range();
      this.clientId();
      if (first) {
        first = false;
        return;
      }
      untracked(() => this.calendar.reload());
    });
    // Clients seen so far stay in the filter (and keep their colour) while the range moves.
    effect(() => {
      const map = new Map(untracked(this.known));
      for (const d of this.calendar.value() ?? []) for (const s of d.sites) map.set(s.clientId, s.clientName);
      if (map.size !== untracked(this.known).size) this.known.set(map);
    });
  }

  readonly label = computed(() => {
    const r = this.range();
    if (this.view() === 'day') return `${DAYS[isoWeekday(r.from) - 1]} ${formatDateKey(r.from)}`;
    if (this.view() === 'week') return `${formatDateKey(r.from)} – ${formatDateKey(r.to)}`;
    return `${MONTHS[Number(r.from.slice(5, 7)) - 1]} ${r.from.slice(0, 4)}`;
  });

  readonly leading = computed(() => (this.view() === 'month' ? Array.from({ length: isoWeekday(this.range().from) - 1 }) : []));

  dayOf(date: DateKey): CalendarDayDto | undefined {
    return this.days().find((d) => d.date === date);
  }

  dayName(date: string): string {
    return DAYS[isoWeekday(date as DateKey) - 1];
  }

  move(step: number): void {
    const a = this.anchor();
    if (this.view() === 'day') this.anchor.set(addDaysToKey(a, step));
    else if (this.view() === 'week') this.anchor.set(addDaysToKey(a, 7 * step));
    else {
      const [y, m] = a.split('-').map(Number);
      const month = m - 1 + step;
      const date = new Date(Date.UTC(y, month, 1));
      this.anchor.set(date.toISOString().slice(0, 10) as DateKey);
    }
  }

  color(clientId: string): string {
    if (!this.colors.has(clientId)) this.colors.set(clientId, PALETTE[this.colors.size % PALETTE.length]);
    return this.colors.get(clientId)!;
  }

  perClient(d: CalendarDayDto): { clientId: string; count: number }[] {
    const counts = new Map<string, number>();
    for (const s of d.sites) counts.set(s.clientId, (counts.get(s.clientId) ?? 0) + s.entries.length);
    return [...counts].map(([clientId, count]) => ({ clientId, count }));
  }

  /** «PADILLA MORA LUIS ENRIQUE» → «Padilla L.» */
  short(name: string): string {
    const words = name.split(' ');
    const cap = (w: string) => w.charAt(0) + w.slice(1).toLowerCase();
    return words.length > 2 ? `${cap(words[0])} ${words[2].charAt(0)}.` : cap(words[0]);
  }

  /** Expected but away: vacation, incapacidad or a suspension. */
  isAway(a: AttendanceStatus | null | undefined): boolean {
    return a === 'vacation' || a === 'sick_leave' || a === 'leave_unpaid';
  }
}
