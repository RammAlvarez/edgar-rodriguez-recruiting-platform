using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PLAN P5 / D69: every Administrador existing before the <c>recruiting</c> grant keeps Reclutamiento. A migration of
    /// its own because Postgres refuses a new enum value in the transaction that added it.
    /// </summary>
    public partial class RecruitingGrantBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO platform.user_permissions (user_id, module, client_id)
                SELECT u.uid, 'recruiting', NULL
                FROM platform.users u
                WHERE u.role = 'administrator'
                  AND NOT EXISTS (SELECT 1 FROM platform.user_permissions p WHERE p.user_id = u.uid AND p.module = 'recruiting');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM platform.user_permissions WHERE module = 'recruiting';");
        }
    }
}
