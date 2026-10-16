import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { DateKeyPipe, TimestampPipe } from '@platform/ui/pipes';
import type {
  ClientOpeningsReportDto,
  OpenOpeningsReportDto,
  RecruiterPerformanceReportDto,
  SourcesReportDto,
  StageFunnelReportDto,
  SummaryMetricsDto,
  SummaryReportDto,
} from '../data/api-types';
import { PRIORITY, SOURCE, STAGE } from '../data/recruiting-labels';
import { average, count, delta, percent } from './report-format';

/** Shared table look of the six bodies (dense, numbers right-aligned, group and total rows shaded). */
const TABLE_STYLES = `
  table {
    border-collapse: collapse;
    font: var(--mat-sys-body-small);
  }
  th,
  td {
    padding: 8px 12px;
    border-bottom: 1px solid var(--mat-sys-outline-variant);
    text-align: left;
    vertical-align: top;
  }
  th.num,
  td.num {
    white-space: nowrap;
  }
  th {
    font: var(--mat-sys-label-medium);
    color: var(--mat-sys-on-surface-variant);
  }
  tbody tr:last-child td {
    border-bottom: 0;
  }
  tr.group td,
  tr.total td {
    background: var(--mat-sys-surface-container);
    font-weight: 500;
  }
  .nowrap {
    white-space: nowrap;
  }
`;

const FORMAT = { percent, average, count };

/** 1 · Rendimiento por reclutador: a total row per recruiter, then her vacantes (click opens the vacante). */
@Component({
  selector: 'app-report-recruiter-performance',
  imports: [DateKeyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Vacante</th><th>Cliente</th><th>Puesto</th><th>Fecha de asignación</th><th class="num">Presentados</th>
            <th class="num">Aptos</th><th class="num">Contratados</th><th class="num">% conversión</th><th class="num">Días para cubrir</th>
          </tr>
        </thead>
        <tbody>
          @for (g of data().recruiters; track g.recruiterId) {
            <tr class="group">
              <td colspan="3">{{ g.recruiterName }}</td>
              <td colspan="6">
                <span class="facts">
                  <span><strong>{{ g.assigned }}</strong> {{ +g.assigned === 1 ? 'vacante asignada' : 'vacantes asignadas' }}</span>
                  <span><strong>{{ g.filled }}</strong> {{ +g.filled === 1 ? 'cubierta' : 'cubiertas' }}</span>
                  <span><strong>{{ f.percent(g.effectiveness) }}</strong> efectividad</span>
                  <span><strong>{{ f.average(g.avgDaysToFill) }}</strong> días promedio</span>
                </span>
              </td>
            </tr>
            @for (o of g.openings; track o.openingId) {
              <tr class="clickable" (click)="open(o.openingId)">
                <td>{{ o.openingName }}</td>
                <td>{{ o.clientName }}</td>
                <td>{{ o.positionName }}</td>
                <td class="nowrap">{{ o.assignedAt | dateKey }}@if (o.isLead) { <span class="muted"> · líder</span> }</td>
                <td class="num">{{ o.presented }}</td>
                <td class="num">{{ o.suitable }}</td>
                <td class="num">{{ o.hired }}</td>
                <td class="num">{{ f.percent(o.conversion) }}</td>
                <td class="num">{{ f.count(o.daysToFill) }}</td>
              </tr>
            }
          } @empty {
            <tr class="empty-row"><td colspan="9">Sin vacantes asignadas en el rango.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [
    TABLE_STYLES,
    `.facts { display: flex; flex-wrap: wrap; gap: 4px 24px; font-weight: 400; color: var(--mat-sys-on-surface-variant); }
     .facts strong { color: var(--mat-sys-on-surface); }`,
  ],
})
export class RecruiterPerformanceBody {
  readonly data = input.required<RecruiterPerformanceReportDto>();
  readonly f = FORMAT;
  private readonly router = inject(Router);

  open(id: string): void {
    void this.router.navigate(['/vacantes', id]);
  }
}

interface Indicator {
  label: string;
  pick: (m: SummaryMetricsDto) => number | string | null;
  kind: 'count' | 'rate' | 'average';
}

const INDICATORS: readonly Indicator[] = [
  { label: 'Vacantes registradas', pick: (m) => m.openings, kind: 'count' },
  { label: 'Abiertas', pick: (m) => m.open, kind: 'count' },
  { label: 'En proceso', pick: (m) => m.inProgress, kind: 'count' },
  { label: 'Cubiertas', pick: (m) => m.filled, kind: 'count' },
  { label: 'Canceladas', pick: (m) => m.cancelled, kind: 'count' },
  { label: 'Pausadas', pick: (m) => m.paused, kind: 'count' },
  { label: 'Candidatos capturados', pick: (m) => m.candidates, kind: 'count' },
  { label: 'Aptos', pick: (m) => m.suitable, kind: 'count' },
  { label: 'Contratados', pick: (m) => m.hired, kind: 'count' },
  { label: 'Conversión', pick: (m) => m.conversion, kind: 'rate' },
  { label: 'Días promedio de cobertura', pick: (m) => m.avgDaysToFill, kind: 'average' },
];

function shown(value: number | string | null, kind: Indicator['kind']): string {
  return kind === 'rate' ? percent(value as string | null) : kind === 'average' ? average(value as string | null) : count(value);
}

/** 2 · Resumen: tiles with the Δ against the previous equal range, and the same indicators per client. */
@Component({
  selector: 'app-report-summary',
  imports: [DateKeyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kpis">
      @for (t of tiles(); track t.label) {
        <div class="kpi">
          <div class="label">{{ t.label }}</div>
          <div class="value">{{ t.value }}</div>
          <div class="delta" [class]="t.tone">{{ t.delta }}</div>
        </div>
      }
    </div>
    <p class="muted note">Rango anterior: {{ data().previousFrom | dateKey }} – {{ data().previousTo | dateKey }}</p>
    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Cliente</th>
            @for (i of indicators; track i.label) {
              <th class="num">{{ i.label }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (c of data().clients; track c.clientId) {
            <tr>
              <td>{{ c.clientName }}</td>
              @for (i of indicators; track i.label) {
                <td class="num">{{ cell(c.metrics, i) }}</td>
              }
            </tr>
          } @empty {
            <tr class="empty-row"><td [attr.colspan]="indicators.length + 1">Sin vacantes registradas en el rango.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [
    TABLE_STYLES,
    `.delta { font: var(--mat-sys-label-small); color: var(--mat-sys-on-surface-variant); }
     .delta.up { color: var(--mat-sys-primary); }
     .delta.down { color: var(--mat-sys-error); }
     .note { margin: -8px 0 12px; font: var(--mat-sys-body-small); }`,
  ],
})
export class SummaryBody {
  readonly data = input.required<SummaryReportDto>();
  readonly indicators = INDICATORS;
  readonly tiles = computed(() =>
    INDICATORS.map((i) => {
      const d = delta(i.pick(this.data().current), i.pick(this.data().previous), i.kind);
      return { label: i.label, value: shown(i.pick(this.data().current), i.kind), delta: d.text, tone: d.tone };
    }),
  );

  cell(m: SummaryMetricsDto, i: Indicator): string {
    return shown(i.pick(m), i.kind);
  }
}

/** 3 · Vacantes abiertas y envejecimiento: oldest first, candidates per etapa, «Sin actividad N días». */
@Component({
  selector: 'app-report-open-openings',
  imports: [TimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Vacante</th><th>Cliente</th><th>Puesto</th><th>Prioridad</th><th>Solicitadas / Contratadas</th>
            <th class="num">Días abierta</th><th>Etapas</th><th>Último movimiento</th><th>Reclutador</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (o of data().openings; track o.openingId) {
            <tr class="clickable" (click)="open(o.openingId)">
              <td>{{ o.openingName }}</td>
              <td>{{ o.clientName }}</td>
              <td>{{ o.positionName }}</td>
              <td>{{ priority[o.priority] }}</td>
              <td>{{ o.hired }} de {{ o.requested }}</td>
              <td class="num"><strong>{{ o.daysOpen }}</strong></td>
              <td>
                <span class="stages">
                  <span class="chip">{{ stage.new.label }} {{ o.new }}</span>
                  <span class="chip">{{ stage.contacted.label }} {{ o.contacted }}</span>
                  <span class="chip">{{ stage.interview.label }} {{ o.interview }}</span>
                  <span class="chip">{{ stage.suitable.label }} {{ o.suitable }}</span>
                </span>
              </td>
              <td class="nowrap">{{ o.lastActivityAt | timestamp }}</td>
              <td>{{ o.leadRecruiterName ?? 'Sin líder' }}</td>
              <td>
                @if (o.stale) {
                  <span class="pill negative nowrap">Sin actividad {{ o.idleDays }} días</span>
                }
              </td>
            </tr>
          } @empty {
            <tr class="empty-row"><td colspan="10">Sin vacantes abiertas con los filtros actuales.</td></tr>
          }
        </tbody>
      </table>
    </div>
    <p class="muted note">«Sin actividad» marca las vacantes con más de {{ data().staleAfterDays }} días sin movimiento.</p>
  `,
  styles: [
    TABLE_STYLES,
    `.stages { display: flex; gap: 4px; white-space: nowrap; }
     .chip { padding: 2px 8px; border-radius: 8px; background: var(--mat-sys-surface-container-highest); font: var(--mat-sys-label-small); }
     .note { margin-top: 8px; font: var(--mat-sys-body-small); }`,
  ],
})
export class OpenOpeningsBody {
  readonly data = input.required<OpenOpeningsReportDto>();
  readonly priority = PRIORITY;
  readonly stage = STAGE;
  private readonly router = inject(Router);

  open(id: string): void {
    void this.router.navigate(['/vacantes', id]);
  }
}

/** 4 · Embudo por etapa: entries, moved on, lost and days per stage; the motivos of the losses beside it. */
@Component({
  selector: 'app-report-stage-funnel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="funnel">
      <div class="table-card grow">
        <table>
          <thead>
            <tr>
              <th>Etapa</th><th class="num">Entraron</th><th class="num">Pasaron a la siguiente</th><th class="num">Perdidos</th>
              <th class="num">% que avanza</th><th class="num">Días promedio en etapa</th>
            </tr>
          </thead>
          <tbody>
            @for (s of data().stages; track s.stage) {
              <tr>
                <td>{{ stage[s.stage].label }}</td>
                <td class="num">{{ s.entered }}</td>
                <td class="num">{{ f.count(s.advanced) }}</td>
                <td class="num" [class.lost]="+s.lost > 0">{{ s.lost }}</td>
                <td class="num">{{ f.percent(s.advanceRate) }}</td>
                <td class="num">{{ f.average(s.avgDaysInStage) }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <section class="losses">
        <h3>Motivos de pérdida</h3>
        <div class="table-card">
          <table>
            <thead>
              <tr><th>Etapa / motivo</th><th class="num">Candidatos</th></tr>
            </thead>
            <tbody>
              @for (l of data().losses; track l.outcome) {
                <tr class="group"><td>{{ stage[l.outcome].label }}</td><td class="num">{{ l.count }}</td></tr>
                @for (r of l.reasons; track r.reason) {
                  <tr><td class="reason">{{ r.reason }}</td><td class="num">{{ r.count }}</td></tr>
                }
              }
            </tbody>
          </table>
        </div>
      </section>
    </div>
    <p class="muted note">
      Cuenta a cada candidato una vez por etapa: su primera entrada en el rango y el movimiento que le siguió.
    </p>
  `,
  styles: [
    TABLE_STYLES,
    `.funnel { display: flex; gap: 16px; align-items: flex-start; flex-wrap: wrap; }
     .grow { flex: 1 1 480px; min-width: 0; }
     .losses { flex: 0 1 340px; min-width: 0; }
     .losses table { width: 100%; }
     h3 { margin: 0 0 8px; font: var(--mat-sys-title-small); }
     .reason { padding-left: 28px; }
     .lost { color: var(--mat-sys-error); }
     .note { margin-top: 8px; font: var(--mat-sys-body-small); }`,
  ],
})
export class StageFunnelBody {
  readonly data = input.required<StageFunnelReportDto>();
  readonly stage = STAGE;
  readonly f = FORMAT;
}

/** 5 · Fuentes de reclutamiento, best conversion first. */
@Component({
  selector: 'app-report-sources',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Fuente</th><th class="num">Candidatos</th><th class="num">Aptos</th><th class="num">Contratados</th>
            <th class="num">% conversión</th><th class="num">Días promedio a contratación</th>
          </tr>
        </thead>
        <tbody>
          @for (s of data().sources; track s.source) {
            <tr>
              <td>{{ s.source ? source[s.source] : 'Sin fuente' }}</td>
              <td class="num">{{ s.candidates }}</td>
              <td class="num">{{ s.suitable }}</td>
              <td class="num">{{ s.hired }}</td>
              <td class="num">
                <span class="bar-cell">
                  <span class="bar"><span [style.width.%]="width(s.conversion)"></span></span>
                  <span class="pct">{{ f.percent(s.conversion) }}</span>
                </span>
              </td>
              <td class="num">{{ f.average(s.avgDaysToHire) }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [
    TABLE_STYLES,
    `.bar-cell { display: inline-flex; align-items: center; gap: 8px; }
     .bar { width: 80px; height: 6px; border-radius: 3px; overflow: hidden; background: var(--mat-sys-surface-variant); }
     .bar span { display: block; height: 100%; background: var(--mat-sys-primary); }
     .pct { width: 40px; }`,
  ],
})
export class SourcesBody {
  readonly data = input.required<SourcesReportDto>();
  readonly source = SOURCE;
  readonly f = FORMAT;

  width(conversion: string | null): number {
    return conversion == null ? 0 : Number(conversion) * 100;
  }
}

/** 6 · Vacantes por cliente with its total row. */
@Component({
  selector: 'app-report-client-openings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Cliente</th><th class="num">Vacantes</th><th class="num">Headcount solicitado</th><th class="num">Contratados</th>
            <th class="num">Fill rate</th><th class="num">Canceladas</th><th class="num">Días promedio de cobertura</th>
            <th class="num">Candidatos presentados</th>
          </tr>
        </thead>
        <tbody>
          @for (c of rows(); track c.clientId ?? 'total') {
            <tr [class.total]="c.clientId === null">
              <td>{{ c.clientName }}</td>
              <td class="num">{{ c.openings }}</td>
              <td class="num">{{ c.headcountRequested }}</td>
              <td class="num">{{ c.hired }}</td>
              <td class="num">{{ f.percent(c.fillRate) }}</td>
              <td class="num">{{ c.cancelled }}</td>
              <td class="num">{{ f.average(c.avgDaysToFill) }}</td>
              <td class="num">{{ c.candidatesPresented }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [TABLE_STYLES],
})
export class ClientOpeningsBody {
  readonly data = input.required<ClientOpeningsReportDto>();
  readonly f = FORMAT;
  readonly rows = computed(() => [...this.data().clients, this.data().total]);
}
