using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reclutamiento and Nómina are separate businesses: a Contratado candidate becomes the client's employee, not one of
    /// Personal, so «Convertir en empleado» and its two link columns go. Dropping <c>candidates.employee_id</c> also drops
    /// the raw-SQL FK <c>fk_candidates_employees_employee_id</c> (PayrollSql); Down puts it back.
    /// </summary>
    public partial class SeparateRecruitingFromPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_employees_candidates_candidate_id",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_employees_candidate_id",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "candidate_id",
                schema: "platform",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "employee_id",
                schema: "platform",
                table: "candidates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "candidate_id",
                schema: "platform",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "employee_id",
                schema: "platform",
                table: "candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_candidate_id",
                schema: "platform",
                table: "employees",
                column: "candidate_id");

            migrationBuilder.AddForeignKey(
                name: "fk_employees_candidates_candidate_id",
                schema: "platform",
                table: "employees",
                column: "candidate_id",
                principalSchema: "platform",
                principalTable: "candidates",
                principalColumn: "uid",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                ALTER TABLE platform.candidates ADD CONSTRAINT fk_candidates_employees_employee_id
                    FOREIGN KEY (employee_id) REFERENCES platform.employees (uid) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
                """);
        }
    }
}
