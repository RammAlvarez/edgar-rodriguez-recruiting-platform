using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence.MigrationSql;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>The reporting views of DATA_MODEL §4.7 (S6); no table changes.</summary>
    public partial class RecruitingReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecruitingReportsSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecruitingReportsSql.Down);
        }
    }
}
