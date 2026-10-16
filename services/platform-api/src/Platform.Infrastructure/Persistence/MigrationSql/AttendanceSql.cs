namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>S8: <c>attendance_unlocks</c> is append-only like the other log tables (PLAN D53).</summary>
public static class AttendanceSql
{
    public const string Up = """
        CREATE TRIGGER trg_attendance_unlocks_append_only BEFORE UPDATE OR DELETE ON platform.attendance_unlocks
            FOR EACH ROW EXECUTE FUNCTION platform.reject_log_change();
        """;

    public const string Down = """
        DROP TRIGGER trg_attendance_unlocks_append_only ON platform.attendance_unlocks;
        """;
}
