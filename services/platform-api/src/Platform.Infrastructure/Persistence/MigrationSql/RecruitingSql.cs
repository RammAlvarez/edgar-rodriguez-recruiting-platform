namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>DATA_MODEL §4.1 (with E9): the "Cliente – Puesto" name of every vacante.</summary>
internal static class RecruitingSql
{
    public const string Up = """
        -- The solicitud date is appended when the client has more than one open / in-process vacante for the same puesto.
        CREATE VIEW platform.job_opening_display AS
        SELECT o.uid AS id,
               cl.short_name || ' – ' || o.position_name
               || CASE WHEN count(*) FILTER (WHERE o.status IN ('open', 'in_progress'))
                            OVER (PARTITION BY o.client_id, lower(o.position_name)) > 1
                       THEN ' · ' || to_char(o.requested_at, 'DD/MM/YYYY') ELSE '' END AS display_name
        FROM platform.job_openings o JOIN platform.clients cl ON cl.uid = o.client_id;
        """;

    public const string Down = """
        DROP VIEW platform.job_opening_display;
        """;
}
