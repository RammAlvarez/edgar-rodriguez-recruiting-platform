using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BatchRowClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                schema: "platform",
                table: "registration_batch_rows",
                type: "uuid",
                nullable: true);

            // Rows generated before this column: the employee's client today is the best record of the client they were made for.
            migrationBuilder.Sql("""
                UPDATE platform.registration_batch_rows r
                SET client_id = g.client_id
                FROM platform.employees e
                JOIN platform.payroll_groups g ON g.uid = e.payroll_group_id
                WHERE e.uid = r.employee_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                schema: "platform",
                table: "registration_batch_rows",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_registration_batch_rows_client_id",
                schema: "platform",
                table: "registration_batch_rows",
                column: "client_id");

            migrationBuilder.AddForeignKey(
                name: "fk_registration_batch_rows_clients_client_id",
                schema: "platform",
                table: "registration_batch_rows",
                column: "client_id",
                principalSchema: "platform",
                principalTable: "clients",
                principalColumn: "uid",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_registration_batch_rows_clients_client_id",
                schema: "platform",
                table: "registration_batch_rows");

            migrationBuilder.DropIndex(
                name: "ix_registration_batch_rows_client_id",
                schema: "platform",
                table: "registration_batch_rows");

            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "platform",
                table: "registration_batch_rows");
        }
    }
}
