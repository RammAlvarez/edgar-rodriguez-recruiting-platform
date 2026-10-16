namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// S9: a paid period is frozen (DATA_MODEL §5.10, §5.12) — its lines, their items and its incidents refuse insert, update
/// and delete at the database, whatever the API does. Corrections are retroactivos in the next period.
/// </summary>
public static class PrepayrollSql
{
    public const string Up = """
        CREATE FUNCTION platform.reject_paid_period_change() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE period uuid;
        DECLARE moved_from uuid;
        BEGIN
            IF TG_TABLE_NAME = 'prepayroll_line_items' THEN
                SELECT payroll_period_id INTO period FROM platform.prepayroll_lines
                WHERE uid = COALESCE(NEW.prepayroll_line_id, OLD.prepayroll_line_id);
            ELSE
                period := COALESCE(NEW.payroll_period_id, OLD.payroll_period_id);
                -- A row is not moved out of a paid period either.
                IF TG_OP = 'UPDATE' THEN
                    moved_from := OLD.payroll_period_id;
                END IF;
            END IF;
            IF EXISTS (SELECT 1 FROM platform.payroll_periods WHERE uid IN (period, moved_from) AND status = 'paid') THEN
                RAISE EXCEPTION 'The period is paid; its prenómina cannot change (%).', TG_TABLE_NAME USING ERRCODE = 'P0001';
            END IF;
            RETURN COALESCE(NEW, OLD);
        END $$;
        CREATE TRIGGER trg_prepayroll_lines_paid BEFORE INSERT OR UPDATE OR DELETE ON platform.prepayroll_lines
            FOR EACH ROW EXECUTE FUNCTION platform.reject_paid_period_change();
        CREATE TRIGGER trg_prepayroll_line_items_paid BEFORE INSERT OR UPDATE OR DELETE ON platform.prepayroll_line_items
            FOR EACH ROW EXECUTE FUNCTION platform.reject_paid_period_change();
        CREATE TRIGGER trg_incidents_paid BEFORE INSERT OR UPDATE OR DELETE ON platform.incidents
            FOR EACH ROW EXECUTE FUNCTION platform.reject_paid_period_change();
        """;

    public const string Down = """
        DROP TRIGGER trg_incidents_paid ON platform.incidents;
        DROP TRIGGER trg_prepayroll_line_items_paid ON platform.prepayroll_line_items;
        DROP TRIGGER trg_prepayroll_lines_paid ON platform.prepayroll_lines;
        DROP FUNCTION platform.reject_paid_period_change();
        """;
}
