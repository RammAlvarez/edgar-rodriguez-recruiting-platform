namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>DATA_MODEL §3.10: the completeness view and the deferred version link of <c>person_documents</c>.</summary>
internal static class PeopleSql
{
    public const string Up = """
        -- A re-upload frees the "current" slot of the previous version before the new row exists; the link is checked at commit.
        ALTER TABLE platform.person_documents
            ALTER CONSTRAINT fk_person_documents_person_documents_superseded_by DEFERRABLE INITIALLY DEFERRED;

        -- % of a template's required documents whose current version is delivered or validated (PLAN D66), per person and
        -- template; truncated to two decimals. A template without required items is complete.
        CREATE VIEW platform.person_document_completeness AS
        SELECT p.uid AS person_id,
               t.uid AS template_id,
               CASE WHEN count(*) FILTER (WHERE i.is_required) = 0 THEN 100.00
                    ELSE trunc(100.0 * count(*) FILTER (WHERE i.is_required AND d.status IN ('delivered', 'validated'))
                               / count(*) FILTER (WHERE i.is_required), 2)
               END::numeric(5,2) AS completeness
        FROM platform.people p
        CROSS JOIN platform.document_templates t
        JOIN platform.document_template_items i ON i.template_id = t.uid
        LEFT JOIN platform.person_documents d
               ON d.person_id = p.uid AND d.document_type_id = i.document_type_id AND d.superseded_by IS NULL
        GROUP BY p.uid, t.uid;
        """;

    public const string Down = """
        DROP VIEW platform.person_document_completeness;
        """;
}
