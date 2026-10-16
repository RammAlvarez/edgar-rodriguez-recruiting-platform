using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence.MigrationSql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Payroll.Engine;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Prepayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .Annotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .Annotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .Annotation("Npgsql:Enum:platform.card_request_status", "queued,in_batch,uploaded,received,cancelled")
                .Annotation("Npgsql:Enum:platform.concept_calc", "fixed_amount,qty_times_hourly_rate,qty_times_hourly_rate_x2,qty_times_hourly_rate_x3,qty_times_daily_salary_x2,percent_of_daily_salary,per_day_worked,manual")
                .Annotation("Npgsql:Enum:platform.concept_kind", "earning,deduction")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.employee_status", "active,vacation,sick_leave,suspended,terminated")
                .Annotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .Annotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .Annotation("Npgsql:Enum:platform.item_source", "attendance,incident,position,manual")
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.period_status", "capturing,in_review,paid")
                .Annotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .OldAnnotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .OldAnnotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .OldAnnotation("Npgsql:Enum:platform.card_request_status", "queued,in_batch,uploaded,received,cancelled")
                .OldAnnotation("Npgsql:Enum:platform.concept_calc", "fixed_amount,qty_times_hourly_rate,qty_times_hourly_rate_x2,qty_times_hourly_rate_x3,qty_times_daily_salary_x2,percent_of_daily_salary,per_day_worked,manual")
                .OldAnnotation("Npgsql:Enum:platform.concept_kind", "earning,deduction")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.employee_status", "active,vacation,sick_leave,suspended,terminated")
                .OldAnnotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .OldAnnotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .OldAnnotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .OldAnnotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .OldAnnotation("Npgsql:Enum:platform.period_status", "capturing,in_review,paid")
                .OldAnnotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "incidents",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payroll_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pay_concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjusted_concept_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    unit_value = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    during_review = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidents", x => x.id);
                    table.UniqueConstraint("ak_incidents_uid", x => x.uid);
                    table.CheckConstraint("ck_incidents_amount", "amount >= 0 OR adjusted_concept_id IS NOT NULL");
                    table.CheckConstraint("ck_incidents_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_incidents_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_pay_concepts_adjusted_concept_id",
                        column: x => x.adjusted_concept_id,
                        principalSchema: "platform",
                        principalTable: "pay_concepts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_pay_concepts_pay_concept_id",
                        column: x => x.pay_concept_id,
                        principalSchema: "platform",
                        principalTable: "pay_concepts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_payroll_periods_origin_period_id",
                        column: x => x.origin_period_id,
                        principalSchema: "platform",
                        principalTable: "payroll_periods",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "platform",
                        principalTable: "payroll_periods",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_users_reported_by",
                        column: x => x.reported_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prepayroll_lines",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_name = table.Column<string>(type: "text", nullable: false),
                    shift = table.Column<string>(type: "text", nullable: false),
                    hired_at = table.Column<DateOnly>(type: "date", nullable: false),
                    days_worked = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    holidays_worked = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    holidays_entitled = table.Column<int>(type: "integer", nullable: false),
                    partial_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    overtime_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    overtime_double_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    overtime_triple_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    worked_sunday = table.Column<bool>(type: "boolean", nullable: false),
                    daily_salary_pocket = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    daily_salary_imss = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    gross_earnings = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    total_net_exact = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    total_net = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    comments = table.Column<string>(type: "text", nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prepayroll_lines", x => x.id);
                    table.UniqueConstraint("ak_prepayroll_lines_uid", x => x.uid);
                    table.CheckConstraint("ck_prepayroll_lines_net", "total_net_exact = gross_earnings - total_deductions");
                    table.CheckConstraint("ck_prepayroll_lines_payable", "total_net = trunc(total_net_exact, 2)");
                    table.ForeignKey(
                        name: "fk_prepayroll_lines_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prepayroll_lines_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "platform",
                        principalTable: "payroll_periods",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prepayroll_line_items",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    prepayroll_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    unit_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    source = table.Column<ItemSource>(type: "platform.item_source", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prepayroll_line_items", x => x.id);
                    table.UniqueConstraint("ak_prepayroll_line_items_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_prepayroll_line_items_incidents_incident_id",
                        column: x => x.incident_id,
                        principalSchema: "platform",
                        principalTable: "incidents",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prepayroll_line_items_pay_concepts_pay_concept_id",
                        column: x => x.pay_concept_id,
                        principalSchema: "platform",
                        principalTable: "pay_concepts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prepayroll_line_items_prepayroll_lines_prepayroll_line_id",
                        column: x => x.prepayroll_line_id,
                        principalSchema: "platform",
                        principalTable: "prepayroll_lines",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_adjusted_concept_id",
                schema: "platform",
                table: "incidents",
                column: "adjusted_concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_employee_id",
                schema: "platform",
                table: "incidents",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_origin_period_id",
                schema: "platform",
                table: "incidents",
                column: "origin_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_pay_concept_id",
                schema: "platform",
                table: "incidents",
                column: "pay_concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_payroll_period_id_employee_id",
                schema: "platform",
                table: "incidents",
                columns: new[] { "payroll_period_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_reported_by",
                schema: "platform",
                table: "incidents",
                column: "reported_by");

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_line_items_incident_id",
                schema: "platform",
                table: "prepayroll_line_items",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_line_items_pay_concept_id",
                schema: "platform",
                table: "prepayroll_line_items",
                column: "pay_concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_line_items_prepayroll_line_id",
                schema: "platform",
                table: "prepayroll_line_items",
                column: "prepayroll_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_lines_employee_id",
                schema: "platform",
                table: "prepayroll_lines",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_lines_payroll_period_id_employee_id",
                schema: "platform",
                table: "prepayroll_lines",
                columns: new[] { "payroll_period_id", "employee_id" },
                unique: true);

            migrationBuilder.Sql(PrepayrollSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PrepayrollSql.Down);

            migrationBuilder.DropTable(
                name: "prepayroll_line_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "incidents",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "prepayroll_lines",
                schema: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .Annotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .Annotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .Annotation("Npgsql:Enum:platform.card_request_status", "queued,in_batch,uploaded,received,cancelled")
                .Annotation("Npgsql:Enum:platform.concept_calc", "fixed_amount,qty_times_hourly_rate,qty_times_hourly_rate_x2,qty_times_hourly_rate_x3,qty_times_daily_salary_x2,percent_of_daily_salary,per_day_worked,manual")
                .Annotation("Npgsql:Enum:platform.concept_kind", "earning,deduction")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.employee_status", "active,vacation,sick_leave,suspended,terminated")
                .Annotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .Annotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.period_status", "capturing,in_review,paid")
                .Annotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .OldAnnotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .OldAnnotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .OldAnnotation("Npgsql:Enum:platform.card_request_status", "queued,in_batch,uploaded,received,cancelled")
                .OldAnnotation("Npgsql:Enum:platform.concept_calc", "fixed_amount,qty_times_hourly_rate,qty_times_hourly_rate_x2,qty_times_hourly_rate_x3,qty_times_daily_salary_x2,percent_of_daily_salary,per_day_worked,manual")
                .OldAnnotation("Npgsql:Enum:platform.concept_kind", "earning,deduction")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.employee_status", "active,vacation,sick_leave,suspended,terminated")
                .OldAnnotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .OldAnnotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .OldAnnotation("Npgsql:Enum:platform.item_source", "attendance,incident,position,manual")
                .OldAnnotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .OldAnnotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .OldAnnotation("Npgsql:Enum:platform.period_status", "capturing,in_review,paid")
                .OldAnnotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
