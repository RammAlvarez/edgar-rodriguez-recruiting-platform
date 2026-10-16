namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// DATA_MODEL §7: <c>audit_log</c> and <c>data_access_log</c>, partitioned by month on <c>occurred_at</c>
/// (a DEFAULT partition catches anything outside the created months), append-only by trigger, no UPDATE/DELETE for the API role.
/// </summary>
internal static class AuditLogSql
{
    public const string Up = """
        CREATE TABLE platform.audit_log (
            id bigint GENERATED ALWAYS AS IDENTITY,
            uid uuid NOT NULL DEFAULT gen_random_uuid(),
            occurred_at timestamptz NOT NULL DEFAULT now(),
            user_id uuid NULL,
            acting_for_user_id uuid NULL,
            action platform.audit_action NOT NULL,
            schema_name text NOT NULL,
            table_name text NOT NULL,
            row_id uuid NULL,
            before jsonb NULL,
            after jsonb NULL,
            request_id text NULL,
            ip inet NULL,
            PRIMARY KEY (id, occurred_at),
            UNIQUE (uid, occurred_at)
        ) PARTITION BY RANGE (occurred_at);
        CREATE INDEX ix_audit_log_row ON platform.audit_log (table_name, row_id, occurred_at);
        CREATE INDEX ix_audit_log_user ON platform.audit_log (user_id, occurred_at);

        CREATE TABLE platform.data_access_log (
            id bigint GENERATED ALWAYS AS IDENTITY,
            uid uuid NOT NULL DEFAULT gen_random_uuid(),
            occurred_at timestamptz NOT NULL DEFAULT now(),
            user_id uuid NULL,
            acting_for_user_id uuid NULL,
            person_id uuid NOT NULL,
            document_id uuid NULL,
            access platform.data_access_kind NOT NULL,
            request_id text NULL,
            PRIMARY KEY (id, occurred_at),
            UNIQUE (uid, occurred_at)
        ) PARTITION BY RANGE (occurred_at);
        CREATE INDEX ix_data_access_log_person ON platform.data_access_log (person_id, occurred_at);

        -- Monthly partitions from go-live through 2027; the DEFAULT partition keeps writes working past that
        -- until more months are added by a migration.
        DO $$
        DECLARE m date := date '2026-10-01';
        BEGIN
            WHILE m < date '2028-01-01' LOOP
                EXECUTE format('CREATE TABLE platform.%I PARTITION OF platform.audit_log FOR VALUES FROM (%L) TO (%L)',
                               'audit_log_' || to_char(m, 'YYYY_MM'), m, m + interval '1 month');
                EXECUTE format('CREATE TABLE platform.%I PARTITION OF platform.data_access_log FOR VALUES FROM (%L) TO (%L)',
                               'data_access_log_' || to_char(m, 'YYYY_MM'), m, m + interval '1 month');
                m := m + interval '1 month';
            END LOOP;
        END $$;
        CREATE TABLE platform.audit_log_default PARTITION OF platform.audit_log DEFAULT;
        CREATE TABLE platform.data_access_log_default PARTITION OF platform.data_access_log DEFAULT;

        CREATE FUNCTION platform.reject_log_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION '% is append-only', TG_TABLE_NAME USING ERRCODE = 'insufficient_privilege';
        END $$;
        CREATE TRIGGER trg_audit_log_append_only BEFORE UPDATE OR DELETE ON platform.audit_log
            FOR EACH ROW EXECUTE FUNCTION platform.reject_log_change();
        CREATE TRIGGER trg_data_access_log_append_only BEFORE UPDATE OR DELETE ON platform.data_access_log
            FOR EACH ROW EXECUTE FUNCTION platform.reject_log_change();

        -- Default privileges grant DML on new tables; the logs are insert + select only.
        DO $$
        BEGIN
            IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'platform_api') THEN
                REVOKE UPDATE, DELETE, TRUNCATE ON platform.audit_log, platform.data_access_log FROM platform_api;
                GRANT SELECT, INSERT ON platform.audit_log, platform.data_access_log TO platform_api;
            END IF;
        END $$;
        """;

    public const string Down = """
        DROP TABLE platform.data_access_log;
        DROP TABLE platform.audit_log;
        DROP FUNCTION platform.reject_log_change();
        """;
}
