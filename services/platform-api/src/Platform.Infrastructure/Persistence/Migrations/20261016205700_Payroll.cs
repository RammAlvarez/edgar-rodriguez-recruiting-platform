using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence.MigrationSql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Core.Catalogs;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Staff;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Payroll : Migration
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
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.period_type", "weekly,biweekly")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .OldAnnotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .OldAnnotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .OldAnnotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .OldAnnotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .OldAnnotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .OldAnnotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "pay_concepts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_es = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<ConceptKind>(type: "platform.concept_kind", nullable: false),
                    calc = table.Column<ConceptCalc>(type: "platform.concept_calc", nullable: false),
                    default_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    default_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    export_column = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pay_concepts", x => x.id);
                    table.UniqueConstraint("ak_pay_concepts_uid", x => x.uid);
                });

            migrationBuilder.CreateTable(
                name: "payroll_groups",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    period_type = table.Column<PeriodType>(type: "platform.period_type", nullable: false, defaultValue: PeriodType.Weekly),
                    cutoff_weekday = table.Column<short>(type: "smallint", nullable: false),
                    shift_hours = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    attendance_lock_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    seventh_day_divisor = table.Column<short>(type: "smallint", nullable: false),
                    sunday_premium_pct = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    holiday_worked_multiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    schedule_description = table.Column<string>(type: "text", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_groups", x => x.id);
                    table.UniqueConstraint("ak_payroll_groups_uid", x => x.uid);
                    table.CheckConstraint("ck_payroll_groups_cutoff", "cutoff_weekday BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_payroll_groups_divisor", "seventh_day_divisor BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_payroll_groups_holiday", "holiday_worked_multiplier BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_payroll_groups_premium", "sunday_premium_pct BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_payroll_groups_shift", "shift_hours > 0 AND shift_hours <= 24");
                    table.ForeignKey(
                        name: "fk_payroll_groups_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "platform",
                        principalTable: "clients",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_group_concepts",
                schema: "platform",
                columns: table => new
                {
                    payroll_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    rate_override = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    amount_override = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_group_concepts", x => new { x.payroll_group_id, x.pay_concept_id });
                    table.ForeignKey(
                        name: "fk_payroll_group_concepts_pay_concepts_pay_concept_id",
                        column: x => x.pay_concept_id,
                        principalSchema: "platform",
                        principalTable: "pay_concepts",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payroll_group_concepts_payroll_group_payroll_group_id",
                        column: x => x.payroll_group_id,
                        principalSchema: "platform",
                        principalTable: "payroll_groups",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.UniqueConstraint("ak_positions_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_positions_payroll_groups_payroll_group_id",
                        column: x => x.payroll_group_id,
                        principalSchema: "platform",
                        principalTable: "payroll_groups",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payroll_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift = table.Column<string>(type: "text", nullable: false),
                    hired_at = table.Column<DateOnly>(type: "date", nullable: false),
                    terminated_at = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<EmployeeStatus>(type: "platform.employee_status", nullable: false, defaultValue: EmployeeStatus.Active),
                    daily_salary_pocket_override = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    daily_salary_imss_override = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    hourly_rate_override = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    days_worked_bonus_override = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                    table.UniqueConstraint("ak_employees_uid", x => x.uid);
                    table.CheckConstraint("ck_employees_overrides", "COALESCE(daily_salary_pocket_override, 0) >= 0 AND COALESCE(daily_salary_imss_override, 0) >= 0 AND COALESCE(hourly_rate_override, 0) >= 0 AND COALESCE(days_worked_bonus_override, 0) >= 0");
                    table.CheckConstraint("ck_employees_terminated", "terminated_at IS NULL OR terminated_at >= hired_at");
                    table.ForeignKey(
                        name: "fk_employees_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "platform",
                        principalTable: "candidates",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_payroll_groups_payroll_group_id",
                        column: x => x.payroll_group_id,
                        principalSchema: "platform",
                        principalTable: "payroll_groups",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "platform",
                        principalTable: "people",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_positions_position_id",
                        column: x => x.position_id,
                        principalSchema: "platform",
                        principalTable: "positions",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_rates",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_salary_pocket = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    daily_salary_imss = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    days_worked_bonus = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_rates", x => x.id);
                    table.UniqueConstraint("ak_position_rates_uid", x => x.uid);
                    table.CheckConstraint("ck_position_rates_amounts", "daily_salary_pocket >= 0 AND daily_salary_imss >= 0 AND hourly_rate >= 0 AND days_worked_bonus >= 0");
                    table.CheckConstraint("ck_position_rates_validity", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_position_rates_positions_position_id",
                        column: x => x.position_id,
                        principalSchema: "platform",
                        principalTable: "positions",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_requests",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank = table.Column<RegistrationBank>(type: "platform.registration_bank", nullable: false),
                    status = table.Column<CardRequestStatus>(type: "platform.card_request_status", nullable: false, defaultValue: CardRequestStatus.Queued),
                    registration_batch_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    card_number_enc = table.Column<byte[]>(type: "bytea", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_requests", x => x.id);
                    table.UniqueConstraint("ak_card_requests_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_card_requests_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_status_periods",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<EmployeeStatus>(type: "platform.employee_status", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    ended_early_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_status_periods", x => x.id);
                    table.UniqueConstraint("ak_employee_status_periods_uid", x => x.uid);
                    table.CheckConstraint("ck_employee_status_periods_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_employee_status_periods_end", "(status = 'terminated') = (end_date IS NULL)");
                    table.CheckConstraint("ck_employee_status_periods_status", "status <> 'active'");
                    table.ForeignKey(
                        name: "fk_employee_status_periods_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_schedules",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekdays = table.Column<short[]>(type: "smallint[]", nullable: false),
                    one_off_date = table.Column<DateOnly>(type: "date", nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    job_description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedules", x => x.id);
                    table.UniqueConstraint("ak_work_schedules_uid", x => x.uid);
                    table.CheckConstraint("ck_work_schedules_kind", "(one_off_date IS NULL AND cardinality(weekdays) > 0) OR (one_off_date IS NOT NULL AND cardinality(weekdays) = 0)");
                    table.CheckConstraint("ck_work_schedules_validity", "valid_to IS NULL OR valid_to >= valid_from");
                    table.CheckConstraint("ck_work_schedules_weekdays", "weekdays <@ ARRAY[1,2,3,4,5,6,7]::smallint[]");
                    table.ForeignKey(
                        name: "fk_work_schedules_client_sites_client_site_id",
                        column: x => x.client_site_id,
                        principalSchema: "platform",
                        principalTable: "client_sites",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_schedules_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<AttendanceStatus>(type: "platform.attendance_status", nullable: false),
                    hours_worked = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    overtime_hours = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 0m),
                    client_site_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_auto_absent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status_period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.UniqueConstraint("ak_attendance_records_uid", x => x.uid);
                    table.CheckConstraint("ck_attendance_records_hours", "hours_worked IS NULL OR hours_worked BETWEEN 0 AND 24");
                    table.CheckConstraint("ck_attendance_records_overtime", "overtime_hours BETWEEN 0 AND 24");
                    table.ForeignKey(
                        name: "fk_attendance_records_client_sites_client_site_id",
                        column: x => x.client_site_id,
                        principalSchema: "platform",
                        principalTable: "client_sites",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "platform",
                        principalTable: "employees",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_employee_status_period_status_period_id",
                        column: x => x.status_period_id,
                        principalSchema: "platform",
                        principalTable: "employee_status_periods",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_client_site_id",
                schema: "platform",
                table: "attendance_records",
                column: "client_site_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_employee_id_work_date",
                schema: "platform",
                table: "attendance_records",
                columns: new[] { "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_status_period_id",
                schema: "platform",
                table: "attendance_records",
                column: "status_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_work_date",
                schema: "platform",
                table: "attendance_records",
                column: "work_date");

            migrationBuilder.CreateIndex(
                name: "ix_card_requests_status",
                schema: "platform",
                table: "card_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_card_requests_open",
                schema: "platform",
                table: "card_requests",
                column: "employee_id",
                unique: true,
                filter: "status IN ('queued', 'in_batch', 'uploaded')");

            migrationBuilder.CreateIndex(
                name: "ix_employee_status_periods_employee_id",
                schema: "platform",
                table: "employee_status_periods",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_candidate_id",
                schema: "platform",
                table: "employees",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_payroll_group_id",
                schema: "platform",
                table: "employees",
                column: "payroll_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_position_id",
                schema: "platform",
                table: "employees",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ux_employees_active_person_group",
                schema: "platform",
                table: "employees",
                columns: new[] { "person_id", "payroll_group_id" },
                unique: true,
                filter: "terminated_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pay_concepts_code",
                schema: "platform",
                table: "pay_concepts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payroll_group_concepts_pay_concept_id",
                schema: "platform",
                table: "payroll_group_concepts",
                column: "pay_concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_groups_client_id_name",
                schema: "platform",
                table: "payroll_groups",
                columns: new[] { "client_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_rates_position_id_valid_from",
                schema: "platform",
                table: "position_rates",
                columns: new[] { "position_id", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_position_rates_current",
                schema: "platform",
                table: "position_rates",
                column: "position_id",
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_positions_payroll_group_id_name",
                schema: "platform",
                table: "positions",
                columns: new[] { "payroll_group_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules_client_site_id",
                schema: "platform",
                table: "work_schedules",
                column: "client_site_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules_employee_id",
                schema: "platform",
                table: "work_schedules",
                column: "employee_id");

            migrationBuilder.Sql(PayrollSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PayrollSql.Down);

            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "card_requests",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payroll_group_concepts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "position_rates",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "work_schedules",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "employee_status_periods",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "pay_concepts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payroll_groups",
                schema: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.bank_account_type", "own_account,debit_card,clabe")
                .Annotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .Annotation("Npgsql:Enum:platform.candidate_stage", "new,contacted,interview,suitable,not_suitable,hired,discarded")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .Annotation("Npgsql:Enum:platform.interview_outcome", "pending,attended,no_show")
                .Annotation("Npgsql:Enum:platform.opening_event_kind", "created,edited,status_changed,recruiter_assigned,recruiter_removed,lead_changed")
                .Annotation("Npgsql:Enum:platform.opening_priority", "high,medium,low")
                .Annotation("Npgsql:Enum:platform.opening_status", "open,in_progress,filled,cancelled,paused")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
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
