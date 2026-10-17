using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence.MigrationSql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Bank.Payments;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Payroll.Prepayroll;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentFiles : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "account_last4",
                schema: "platform",
                table: "prepayroll_lines",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "account_number_enc",
                schema: "platform",
                table: "prepayroll_lines",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<BankAccountType>(
                name: "account_type",
                schema: "platform",
                table: "prepayroll_lines",
                type: "platform.bank_account_type",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bank_id",
                schema: "platform",
                table: "prepayroll_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "holder_name",
                schema: "platform",
                table: "prepayroll_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<PaymentRowStatus>(
                name: "payment_status",
                schema: "platform",
                table: "prepayroll_lines",
                type: "platform.payment_row_status",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_bank_numbers",
                schema: "platform",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank = table.Column<RegistrationBank>(type: "platform.registration_bank", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_bank_numbers", x => new { x.employee_id, x.bank });
                    table.ForeignKey(
                        name: "fk_employee_bank_numbers_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paying_accounts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_account_number_enc = table.Column<byte[]>(type: "bytea", nullable: false),
                    charge_account_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    holder_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    holder_rfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    default_charge_legend = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PAGO"),
                    default_credit_legend = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PAGO"),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "MXP"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paying_accounts", x => x.id);
                    table.UniqueConstraint("ak_paying_accounts_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_paying_accounts_banks_bank_id",
                        column: x => x.bank_id,
                        principalSchema: "platform",
                        principalTable: "banks",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registration_batches",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    bank = table.Column<RegistrationBank>(type: "platform.registration_bank", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<BatchStatus>(type: "platform.batch_status", nullable: false, defaultValue: BatchStatus.Generated),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    layout_version = table.Column<string>(type: "text", nullable: false),
                    occupation_code = table.Column<string>(type: "text", nullable: true),
                    product_code = table.Column<string>(type: "text", nullable: true),
                    card_type = table.Column<string>(type: "text", nullable: true),
                    gross_monthly_salary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    branch_to_send = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registration_batches", x => x.id);
                    table.UniqueConstraint("ak_registration_batches_uid", x => x.uid);
                    table.CheckConstraint("ck_registration_batches_rows", "row_count > 0");
                    table.ForeignKey(
                        name: "fk_registration_batches_users_generated_by",
                        column: x => x.generated_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registration_batches_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_batches",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    paying_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<PaymentBatchKind>(type: "platform.payment_batch_kind", nullable: false),
                    payroll_period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<BatchStatus>(type: "platform.batch_status", nullable: false, defaultValue: BatchStatus.Generated),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    frequent_accounts_storage_path = table.Column<string>(type: "text", nullable: true),
                    frequent_accounts_count = table.Column<int>(type: "integer", nullable: false),
                    layout_version = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_batches", x => x.id);
                    table.UniqueConstraint("ak_payment_batches_uid", x => x.uid);
                    table.CheckConstraint("ck_payment_batches_rows", "row_count > 0 AND total_amount > 0");
                    table.ForeignKey(
                        name: "fk_payment_batches_paying_accounts_paying_account_id",
                        column: x => x.paying_account_id,
                        principalSchema: "platform",
                        principalTable: "paying_accounts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_batches_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "platform",
                        principalTable: "payroll_periods",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_batches_users_generated_by",
                        column: x => x.generated_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_batches_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registration_batch_rows",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    card_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registration_batch_rows", x => x.id);
                    table.UniqueConstraint("ak_registration_batch_rows_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_registration_batch_rows_card_requests_card_request_id",
                        column: x => x.card_request_id,
                        principalSchema: "platform",
                        principalTable: "card_requests",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registration_batch_rows_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registration_batch_rows_registration_batches_registration_b",
                        column: x => x.registration_batch_id,
                        principalSchema: "platform",
                        principalTable: "registration_batches",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_batch_rows",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payment_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    prepayroll_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    receiving_bank_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    account_type = table.Column<BankAccountType>(type: "platform.bank_account_type", nullable: false),
                    account_number_enc = table.Column<byte[]>(type: "bytea", nullable: false),
                    account_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    payment_type = table.Column<PaymentType>(type: "platform.payment_type", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    charge_legend = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    credit_legend = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    registers_frequent_account = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<PaymentRowStatus>(type: "platform.payment_row_status", nullable: false, defaultValue: PaymentRowStatus.Pending),
                    rejection_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_batch_rows", x => x.id);
                    table.UniqueConstraint("ak_payment_batch_rows_uid", x => x.uid);
                    table.CheckConstraint("ck_payment_batch_rows_amount", "amount > 0 AND amount = trunc(amount, 2)");
                    table.ForeignKey(
                        name: "fk_payment_batch_rows_payment_batches_payment_batch_id",
                        column: x => x.payment_batch_id,
                        principalSchema: "platform",
                        principalTable: "payment_batches",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_batch_rows_person_bank_accounts_person_bank_account",
                        column: x => x.person_bank_account_id,
                        principalSchema: "platform",
                        principalTable: "person_bank_accounts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_batch_rows_prepayroll_lines_prepayroll_line_id",
                        column: x => x.prepayroll_line_id,
                        principalSchema: "platform",
                        principalTable: "prepayroll_lines",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prepayroll_lines_bank_id",
                schema: "platform",
                table: "prepayroll_lines",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_bank_numbers_bank_employee_number",
                schema: "platform",
                table: "employee_bank_numbers",
                columns: new[] { "bank", "employee_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paying_accounts_bank_id",
                schema: "platform",
                table: "paying_accounts",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batch_rows_payment_batch_id_line_no",
                schema: "platform",
                table: "payment_batch_rows",
                columns: new[] { "payment_batch_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_batch_rows_person_bank_account_id",
                schema: "platform",
                table: "payment_batch_rows",
                column: "person_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batch_rows_prepayroll_line_id",
                schema: "platform",
                table: "payment_batch_rows",
                column: "prepayroll_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batches_generated_by",
                schema: "platform",
                table: "payment_batches",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batches_paying_account_id",
                schema: "platform",
                table: "payment_batches",
                column: "paying_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batches_payroll_period_id",
                schema: "platform",
                table: "payment_batches",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_batches_uploaded_by",
                schema: "platform",
                table: "payment_batches",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_registration_batch_rows_card_request_id",
                schema: "platform",
                table: "registration_batch_rows",
                column: "card_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registration_batch_rows_employee_id",
                schema: "platform",
                table: "registration_batch_rows",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_registration_batch_rows_registration_batch_id_line_no",
                schema: "platform",
                table: "registration_batch_rows",
                columns: new[] { "registration_batch_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registration_batches_generated_by",
                schema: "platform",
                table: "registration_batches",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "ix_registration_batches_uploaded_by",
                schema: "platform",
                table: "registration_batches",
                column: "uploaded_by");

            migrationBuilder.AddForeignKey(
                name: "fk_prepayroll_lines_banks_bank_id",
                schema: "platform",
                table: "prepayroll_lines",
                column: "bank_id",
                principalSchema: "platform",
                principalTable: "banks",
                principalColumn: "uid",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(PaymentFilesSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PaymentFilesSql.Down);

            migrationBuilder.DropForeignKey(
                name: "fk_prepayroll_lines_banks_bank_id",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropTable(
                name: "employee_bank_numbers",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payment_batch_rows",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "registration_batch_rows",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payment_batches",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "registration_batches",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "paying_accounts",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "ix_prepayroll_lines_bank_id",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "account_last4",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "account_number_enc",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "account_type",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "bank_id",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "holder_name",
                schema: "platform",
                table: "prepayroll_lines");

            migrationBuilder.DropColumn(
                name: "payment_status",
                schema: "platform",
                table: "prepayroll_lines");

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
