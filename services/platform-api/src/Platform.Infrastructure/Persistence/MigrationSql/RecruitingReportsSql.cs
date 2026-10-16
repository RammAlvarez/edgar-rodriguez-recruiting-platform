namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// The reporting views of DATA_MODEL §4.7 (SPEC §4.7, PLAN E11/D80), readable by <c>platform_readonly</c> through the default
/// privileges of <c>db/roles.sql</c>. They return counts and dates only: percentages, averages and "today" (America/Monterrey,
/// D70) are computed in the API, so the screen, the Excel and the PDF show the same figures. Every candidate count is taken in
/// a subquery per vacante, so joining the stage history never multiplies it.
/// </summary>
public static class RecruitingReportsSql
{
    public const string Up = """
        -- 1 Rendimiento por reclutador (BD_RECLUTADORES): one row per recruiter × vacante; «presentados» are the candidates
        -- that recruiter captured or owns on that vacante (candidates.recruiter_id).
        CREATE VIEW platform.recruiter_performance AS
        SELECT r.user_id AS recruiter_id, o.uid AS job_opening_id, o.client_id, o.position_name, r.is_lead, r.assigned_at,
               o.requested_at, o.filled_at, o.status, o.headcount_requested,
               count(c.id)                              AS candidates_presented,
               count(c.id) FILTER (WHERE c.is_suitable) AS candidates_suitable,
               count(c.id) FILTER (WHERE c.is_hired)    AS candidates_hired,
               o.filled_at - o.requested_at             AS days_to_fill
        FROM platform.job_opening_recruiters r
        JOIN platform.job_openings o ON o.uid = r.job_opening_id
        LEFT JOIN platform.candidates c ON c.job_opening_id = r.job_opening_id AND c.recruiter_id = r.user_id
        GROUP BY r.id, o.id;

        -- 2 Resumen de reclutamiento (DASHBOARD) and 6 Vacantes por cliente: one row per vacante with its candidate counts.
        CREATE VIEW platform.client_openings AS
        SELECT o.client_id, o.uid AS job_opening_id, o.requested_at, o.status, o.headcount_requested, o.filled_at,
               count(c.id)                              AS candidates_presented,
               count(c.id) FILTER (WHERE c.is_suitable) AS candidates_suitable,
               count(c.id) FILTER (WHERE c.is_hired)    AS hired,
               o.filled_at - o.requested_at             AS days_to_fill
        FROM platform.job_openings o
        LEFT JOIN platform.candidates c ON c.job_opening_id = o.uid
        GROUP BY o.id;

        -- 2 Resumen per client for a range of fecha de solicitud; the Δ is the API calling it for the previous equal range.
        CREATE FUNCTION platform.summary(p_from date, p_to date, p_client uuid DEFAULT NULL, p_recruiter uuid DEFAULT NULL)
        RETURNS TABLE (client_id uuid, openings_total int, openings_open int, openings_in_progress int, openings_filled int,
                       openings_cancelled int, openings_paused int, candidates_total int, candidates_suitable int,
                       candidates_hired int, days_to_fill_total int)
        LANGUAGE sql STABLE AS $$
            SELECT co.client_id,
                   count(*)::int,
                   count(*) FILTER (WHERE co.status = 'open')::int,
                   count(*) FILTER (WHERE co.status = 'in_progress')::int,
                   count(*) FILTER (WHERE co.status = 'filled')::int,
                   count(*) FILTER (WHERE co.status = 'cancelled')::int,
                   count(*) FILTER (WHERE co.status = 'paused')::int,
                   coalesce(sum(co.candidates_presented), 0)::int,
                   coalesce(sum(co.candidates_suitable), 0)::int,
                   coalesce(sum(co.hired), 0)::int,
                   coalesce(sum(co.days_to_fill) FILTER (WHERE co.status = 'filled'), 0)::int
            FROM platform.client_openings co
            WHERE co.requested_at BETWEEN p_from AND p_to
              AND (p_client IS NULL OR co.client_id = p_client)
              AND (p_recruiter IS NULL OR EXISTS (SELECT 1 FROM platform.job_opening_recruiters r
                                                  WHERE r.job_opening_id = co.job_opening_id AND r.user_id = p_recruiter))
            GROUP BY co.client_id
        $$;

        -- 3 Vacantes abiertas y envejecimiento (snapshot): candidates per etapa and the last movement — the latest stage move,
        -- capture or edit of the vacante.
        CREATE VIEW platform.open_openings_aging AS
        SELECT o.uid AS id, o.client_id, o.position_name, o.priority, o.status, o.headcount_requested, o.requested_at,
               o.lead_recruiter_id,
               coalesce(c.hired, 0)     AS hired,
               coalesce(c.new, 0)       AS stage_new,
               coalesce(c.contacted, 0) AS stage_contacted,
               coalesce(c.interview, 0) AS stage_interview,
               coalesce(c.suitable, 0)  AS stage_suitable,
               greatest(h.last_change, o.updated_at) AS last_activity_at
        FROM platform.job_openings o
        LEFT JOIN (SELECT job_opening_id,
                          count(*) FILTER (WHERE is_hired)            AS hired,
                          count(*) FILTER (WHERE stage = 'new')       AS new,
                          count(*) FILTER (WHERE stage = 'contacted') AS contacted,
                          count(*) FILTER (WHERE stage = 'interview') AS interview,
                          count(*) FILTER (WHERE stage = 'suitable')  AS suitable
                   FROM platform.candidates GROUP BY job_opening_id) c ON c.job_opening_id = o.uid
        LEFT JOIN (SELECT c.job_opening_id, max(h.changed_at) AS last_change
                   FROM platform.candidate_stage_history h JOIN platform.candidates c ON c.uid = h.candidate_id
                   GROUP BY c.job_opening_id) h ON h.job_opening_id = o.uid
        WHERE o.status IN ('open', 'in_progress');

        -- 4 Embudo por etapa: every stage move with the move that followed it (its stage and when); a move to No apto or
        -- Descartado carries its motivo in note.
        CREATE VIEW platform.stage_funnel AS
        SELECT h.candidate_id, c.job_opening_id, o.client_id, c.recruiter_id, c.source, h.to_stage AS stage, h.changed_at, h.note,
               lead(h.to_stage)   OVER w AS next_stage,
               lead(h.changed_at) OVER w AS next_changed_at
        FROM platform.candidate_stage_history h
        JOIN platform.candidates c ON c.uid = h.candidate_id
        JOIN platform.job_openings o ON o.uid = c.job_opening_id
        WINDOW w AS (PARTITION BY h.candidate_id ORDER BY h.changed_at, h.id);

        -- 5 Fuentes de reclutamiento: one row per candidate.
        CREATE VIEW platform.source_effectiveness AS
        SELECT c.uid AS candidate_id, c.source, o.client_id, c.recruiter_id, c.captured_at, c.is_suitable, c.is_hired,
               c.hired_at - c.captured_at AS days_to_hire
        FROM platform.candidates c JOIN platform.job_openings o ON o.uid = c.job_opening_id;
        """;

    public const string Down = """
        DROP VIEW platform.source_effectiveness;
        DROP VIEW platform.stage_funnel;
        DROP VIEW platform.open_openings_aging;
        DROP FUNCTION platform.summary(date, date, uuid, uuid);
        DROP VIEW platform.client_openings;
        DROP VIEW platform.recruiter_performance;
        """;
}
