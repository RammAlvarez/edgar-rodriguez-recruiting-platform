using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PuestosAsTyped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_positions_payroll_group_id_name",
                schema: "platform",
                table: "positions");

            migrationBuilder.CreateIndex(
                name: "ix_positions_payroll_group_id",
                schema: "platform",
                table: "positions",
                column: "payroll_group_id");

            // Puestos are shown exactly as typed, so uniqueness must not depend on capitals («Operador» / «OPERADOR» in one group).
            // EF has no expression indexes; the API's unique-violation handling answers «Ya existe…» as before. A database that holds
            // two spellings in one group stops here, to be merged by hand first.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM platform.positions GROUP BY payroll_group_id, lower(name) HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'platform.positions has puestos that differ only in capitals within a group; merge them before this migration';
                  END IF;
                END $$;
                CREATE UNIQUE INDEX ux_positions_group_name ON platform.positions (payroll_group_id, lower(name));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX platform.ux_positions_group_name;");

            migrationBuilder.DropIndex(
                name: "ix_positions_payroll_group_id",
                schema: "platform",
                table: "positions");

            migrationBuilder.CreateIndex(
                name: "ix_positions_payroll_group_id_name",
                schema: "platform",
                table: "positions",
                columns: new[] { "payroll_group_id", "name" },
                unique: true);
        }
    }
}
