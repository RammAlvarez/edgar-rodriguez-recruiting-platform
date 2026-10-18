using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Bank.Payments;
using Platform.Payroll.Prepayroll;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .Annotation("Npgsql:Enum:platform.batch_status", "generated,uploaded_to_bank,completed,cancelled")
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
                .Annotation("Npgsql:Enum:platform.manual_payment_method", "cash,check,other")
                .Annotation("Npgsql:Enum:platform.notification_kind", "document_expiring,document_uploaded,opening_idle,attendance_missing,period_in_review,user_invited")
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.payment_batch_kind", "period_payment,pending_payment,frequent_accounts")
                .Annotation("Npgsql:Enum:platform.payment_row_status", "ready,missing_account,invalid_account,pending,paid,rejected")
                .Annotation("Npgsql:Enum:platform.payment_type", "spei,tef,baz_transfer")
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
                .OldAnnotation("Npgsql:Enum:platform.batch_status", "generated,uploaded_to_bank,completed,cancelled")
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
                .OldAnnotation("Npgsql:Enum:platform.notification_kind", "document_expiring,document_uploaded,opening_idle,attendance_missing,period_in_review,user_invited")
                .OldAnnotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .OldAnnotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .OldAnnotation("Npgsql:Enum:platform.payment_batch_kind", "period_payment,pending_payment,frequent_accounts")
                .OldAnnotation("Npgsql:Enum:platform.payment_row_status", "ready,missing_account,invalid_account,pending,paid,rejected")
                .OldAnnotation("Npgsql:Enum:platform.payment_type", "spei,tef,baz_transfer")
                .OldAnnotation("Npgsql:Enum:platform.period_status", "capturing,in_review,paid")
                .OldAnnotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "manual_payments",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    prepayroll_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<ManualPaymentMethod>(type: "platform.manual_payment_method", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    previous_status = table.Column<PaymentRowStatus>(type: "platform.payment_row_status", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manual_payments", x => x.id);
                    table.UniqueConstraint("ak_manual_payments_uid", x => x.uid);
                    table.CheckConstraint("ck_manual_payments_amount", "amount > 0 AND amount = trunc(amount, 2)");
                    table.ForeignKey(
                        name: "fk_manual_payments_prepayroll_lines_prepayroll_line_id",
                        column: x => x.prepayroll_line_id,
                        principalSchema: "platform",
                        principalTable: "prepayroll_lines",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_manual_payments_prepayroll_line_id",
                schema: "platform",
                table: "manual_payments",
                column: "prepayroll_line_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_payments",
                schema: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.attendance_status", "present,partial,absent,rest_day,rest_day_worked,holiday,holiday_worked,sick_leave,leave_paid,leave_unpaid,vacation")
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .Annotation("Npgsql:Enum:platform.batch_status", "generated,uploaded_to_bank,completed,cancelled")
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
                .Annotation("Npgsql:Enum:platform.notification_kind", "document_expiring,document_uploaded,opening_idle,attendance_missing,period_in_review,user_invited")
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.payment_batch_kind", "period_payment,pending_payment,frequent_accounts")
                .Annotation("Npgsql:Enum:platform.payment_row_status", "ready,missing_account,invalid_account,pending,paid,rejected")
                .Annotation("Npgsql:Enum:platform.payment_type", "spei,tef,baz_transfer")
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
                .OldAnnotation("Npgsql:Enum:platform.batch_status", "generated,uploaded_to_bank,completed,cancelled")
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
                .OldAnnotation("Npgsql:Enum:platform.manual_payment_method", "cash,check,other")
                .OldAnnotation("Npgsql:Enum:platform.notification_kind", "document_expiring,document_uploaded,opening_idle,attendance_missing,period_in_review,user_invited")
                .OldAnnotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .OldAnnotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .OldAnnotation("Npgsql:Enum:platform.payment_batch_kind", "period_payment,pending_payment,frequent_accounts")
                .OldAnnotation("Npgsql:Enum:platform.payment_row_status", "ready,missing_account,invalid_account,pending,paid,rejected")
                .OldAnnotation("Npgsql:Enum:platform.payment_type", "spei,tef,baz_transfer")
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
