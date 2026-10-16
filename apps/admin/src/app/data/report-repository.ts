import { inject, Injectable } from '@angular/core';
import type { DateKey } from '@platform/shared';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type {
  ClientOpeningsReportDto,
  OpenOpeningsReportDto,
  RecruiterPerformanceReportDto,
  RecruitingReport,
  ReportExportDto,
  ReportFormat,
  ReportOptionsDto,
  SourcesReportDto,
  StageFunnelReportDto,
  SummaryReportDto,
} from './api-types';

/** What the screen asks for: the selected report and its filters (`null` = Todos). */
export interface ReportQuery {
  readonly report: RecruitingReport;
  readonly clientId: string | null;
  readonly recruiterId: string | null;
  readonly from: DateKey;
  readonly to: DateKey;
}

/** The body of one report, tagged with which one it is so the template can narrow it. */
export type ReportResult =
  | { readonly report: 'recruiter_performance'; readonly data: RecruiterPerformanceReportDto }
  | { readonly report: 'summary'; readonly data: SummaryReportDto }
  | { readonly report: 'open_openings'; readonly data: OpenOpeningsReportDto }
  | { readonly report: 'stage_funnel'; readonly data: StageFunnelReportDto }
  | { readonly report: 'sources'; readonly data: SourcesReportDto }
  | { readonly report: 'client_openings'; readonly data: ClientOpeningsReportDto };

const PATH: Record<RecruitingReport, string> = {
  recruiter_performance: 'recruiter-performance',
  summary: 'summary',
  open_openings: 'open-openings',
  stage_funnel: 'stage-funnel',
  sources: 'sources',
  client_openings: 'client-openings',
};

/**
 * Reportes de reclutamiento (SPEC §4.7). A report's figures change with every vacante or candidate write, so the query is
 * tagged with those too; report 3 is a snapshot and ignores the range.
 */
@Injectable({ providedIn: 'root' })
export class ReportRepository {
  private readonly api = inject(API_CLIENT);

  options() {
    return liveQuery(() => this.api.get<ReportOptionsDto>('/v1/recruiting-reports/options'), ['clients', 'users', 'openings']);
  }

  report(query: () => ReportQuery) {
    return liveQuery(async (): Promise<ReportResult> => {
      const q = query();
      const range = q.report === 'open_openings' ? {} : { from: q.from, to: q.to };
      const data = await this.api.get(`/v1/recruiting-reports/${PATH[q.report]}`, {
        clientId: q.clientId,
        recruiterId: q.recruiterId,
        ...range,
      });
      return { report: q.report, data } as ReportResult;
    }, ['recruiting-reports', 'openings', 'candidates', 'recruiters']);
  }

  export(query: ReportQuery, format: ReportFormat): Promise<ReportExportDto> {
    return this.api.post('/v1/recruiting-reports/exports', { ...query, format });
  }
}
