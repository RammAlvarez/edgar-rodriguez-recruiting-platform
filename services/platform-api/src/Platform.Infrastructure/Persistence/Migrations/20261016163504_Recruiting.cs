using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Core.People;
using Platform.Infrastructure.Persistence.MigrationSql;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Recruiting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
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
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "job_openings",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    legacy_code = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "current_date"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_name = table.Column<string>(type: "text", nullable: false),
                    headcount_requested = table.Column<int>(type: "integer", nullable: false),
                    monthly_salary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    benefits = table.Column<List<string>>(type: "text[]", nullable: false),
                    schedule = table.Column<string>(type: "text", nullable: true),
                    payment_bank_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    education_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    years_experience = table.Column<int>(type: "integer", nullable: true),
                    required_experience = table.Column<string>(type: "text", nullable: true),
                    gender_requirement = table.Column<Gender>(type: "platform.gender", nullable: false, defaultValue: Gender.Unspecified),
                    age_range = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<OpeningPriority>(type: "platform.opening_priority", nullable: false),
                    status = table.Column<OpeningStatus>(type: "platform.opening_status", nullable: false, defaultValue: OpeningStatus.Open),
                    filled_at = table.Column<DateOnly>(type: "date", nullable: true),
                    lead_recruiter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visible_to_client = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_openings", x => x.id);
                    table.UniqueConstraint("ak_job_opening_uid", x => x.uid);
                    table.CheckConstraint("ck_job_openings_filled", "(status = 'filled') = (filled_at IS NOT NULL)");
                    table.CheckConstraint("ck_job_openings_headcount", "headcount_requested > 0");
                    table.CheckConstraint("ck_job_openings_salary", "monthly_salary IS NULL OR monthly_salary >= 0");
                    table.CheckConstraint("ck_job_openings_years", "years_experience IS NULL OR years_experience >= 0");
                    table.ForeignKey(
                        name: "fk_job_openings_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "platform",
                        principalTable: "clients",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_openings_document_templates_document_template_id",
                        column: x => x.document_template_id,
                        principalSchema: "platform",
                        principalTable: "document_templates",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_openings_education_levels_education_level_id",
                        column: x => x.education_level_id,
                        principalSchema: "platform",
                        principalTable: "education_levels",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_openings_users_lead_recruiter_id",
                        column: x => x.lead_recruiter_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_openings_zones_zone_id",
                        column: x => x.zone_id,
                        principalSchema: "platform",
                        principalTable: "zones",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    legacy_code = table.Column<string>(type: "text", nullable: true),
                    captured_at = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "current_date"),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruiter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<CandidateSource>(type: "platform.candidate_source", nullable: true),
                    stage = table.Column<CandidateStage>(type: "platform.candidate_stage", nullable: false, defaultValue: CandidateStage.New),
                    is_suitable = table.Column<bool>(type: "boolean", nullable: true),
                    is_hired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    hired_at = table.Column<DateOnly>(type: "date", nullable: true),
                    offered_salary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    comments = table.Column<string>(type: "text", nullable: true),
                    discard_reason = table.Column<string>(type: "text", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidates", x => x.id);
                    table.UniqueConstraint("ak_candidates_uid", x => x.uid);
                    table.CheckConstraint("ck_candidates_hired", "is_hired = (stage = 'hired') AND (stage <> 'hired' OR hired_at IS NOT NULL)");
                    table.CheckConstraint("ck_candidates_reason", "stage NOT IN ('not_suitable', 'discarded') OR discard_reason IS NOT NULL");
                    table.CheckConstraint("ck_candidates_salary", "offered_salary IS NULL OR offered_salary >= 0");
                    table.ForeignKey(
                        name: "fk_candidates_job_opening_job_opening_id",
                        column: x => x.job_opening_id,
                        principalSchema: "platform",
                        principalTable: "job_openings",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidates_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "platform",
                        principalTable: "people",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidates_users_recruiter_id",
                        column: x => x.recruiter_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_opening_events",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<OpeningEventKind>(type: "platform.opening_event_kind", nullable: false),
                    from_status = table.Column<OpeningStatus>(type: "platform.opening_status", nullable: true),
                    to_status = table.Column<OpeningStatus>(type: "platform.opening_status", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    recruiter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acting_for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_opening_events", x => x.id);
                    table.UniqueConstraint("ak_job_opening_events_uid", x => x.uid);
                    table.CheckConstraint("ck_job_opening_events_reason", "kind <> 'status_changed' OR to_status NOT IN ('paused', 'cancelled') OR reason IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_job_opening_events_job_openings_job_opening_id",
                        column: x => x.job_opening_id,
                        principalSchema: "platform",
                        principalTable: "job_openings",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_opening_events_users_acting_for_user_id",
                        column: x => x.acting_for_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_opening_events_users_recruiter_user_id",
                        column: x => x.recruiter_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_opening_events_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_opening_recruiters",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateOnly>(type: "date", nullable: false),
                    is_lead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_opening_recruiters", x => x.id);
                    table.UniqueConstraint("ak_job_opening_recruiters_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_job_opening_recruiters_job_openings_job_opening_id",
                        column: x => x.job_opening_id,
                        principalSchema: "platform",
                        principalTable: "job_openings",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_opening_recruiters_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opening_comments",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acting_for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    visible_to_client = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_comments", x => x.id);
                    table.UniqueConstraint("ak_opening_comments_uid", x => x.uid);
                    table.CheckConstraint("ck_opening_comments_body", "length(body) BETWEEN 1 AND 2000");
                    table.ForeignKey(
                        name: "fk_opening_comments_job_openings_job_opening_id",
                        column: x => x.job_opening_id,
                        principalSchema: "platform",
                        principalTable: "job_openings",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_opening_comments_users_acting_for_user_id",
                        column: x => x.acting_for_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opening_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_notes",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acting_for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_html = table.Column<string>(type: "text", nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: true),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_notes", x => x.id);
                    table.UniqueConstraint("ak_candidate_notes_uid", x => x.uid);
                    table.CheckConstraint("ck_candidate_notes_html", "length(content_html) BETWEEN 1 AND 20000");
                    table.ForeignKey(
                        name: "fk_candidate_notes_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "platform",
                        principalTable: "candidates",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_candidate_notes_users_acting_for_user_id",
                        column: x => x.acting_for_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidate_notes_users_author_user_id",
                        column: x => x.author_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_stage_history",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_stage = table.Column<CandidateStage>(type: "platform.candidate_stage", nullable: true),
                    to_stage = table.Column<CandidateStage>(type: "platform.candidate_stage", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    acting_for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_stage_history", x => x.id);
                    table.UniqueConstraint("ak_candidate_stage_history_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_candidate_stage_history_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "platform",
                        principalTable: "candidates",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_candidate_stage_history_users_acting_for_user_id",
                        column: x => x.acting_for_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidate_stage_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    interviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<InterviewOutcome>(type: "platform.interview_outcome", nullable: false, defaultValue: InterviewOutcome.Pending),
                    notes = table.Column<string>(type: "text", nullable: true),
                    acting_for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interviews", x => x.id);
                    table.UniqueConstraint("ak_interviews_uid", x => x.uid);
                    table.ForeignKey(
                        name: "fk_interviews_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "platform",
                        principalTable: "candidates",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interviews_users_acting_for_user_id",
                        column: x => x.acting_for_user_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interviews_users_interviewer_id",
                        column: x => x.interviewer_id,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_notes_acting_for_user_id",
                schema: "platform",
                table: "candidate_notes",
                column: "acting_for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_notes_author_user_id",
                schema: "platform",
                table: "candidate_notes",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_notes_candidate_id_created_at",
                schema: "platform",
                table: "candidate_notes",
                columns: new[] { "candidate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_history_acting_for_user_id",
                schema: "platform",
                table: "candidate_stage_history",
                column: "acting_for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_history_candidate_id_changed_at",
                schema: "platform",
                table: "candidate_stage_history",
                columns: new[] { "candidate_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_history_changed_by",
                schema: "platform",
                table: "candidate_stage_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_job_opening_id_stage",
                schema: "platform",
                table: "candidates",
                columns: new[] { "job_opening_id", "stage" });

            migrationBuilder.CreateIndex(
                name: "ix_candidates_legacy_code",
                schema: "platform",
                table: "candidates",
                column: "legacy_code",
                unique: true,
                filter: "legacy_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_person_id_job_opening_id",
                schema: "platform",
                table: "candidates",
                columns: new[] { "person_id", "job_opening_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_recruiter_id",
                schema: "platform",
                table: "candidates",
                column: "recruiter_id");

            migrationBuilder.CreateIndex(
                name: "ix_interviews_acting_for_user_id",
                schema: "platform",
                table: "interviews",
                column: "acting_for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_interviews_candidate_id_scheduled_at",
                schema: "platform",
                table: "interviews",
                columns: new[] { "candidate_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_interviews_interviewer_id",
                schema: "platform",
                table: "interviews",
                column: "interviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_events_acting_for_user_id",
                schema: "platform",
                table: "job_opening_events",
                column: "acting_for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_events_job_opening_id_occurred_at",
                schema: "platform",
                table: "job_opening_events",
                columns: new[] { "job_opening_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_events_recruiter_user_id",
                schema: "platform",
                table: "job_opening_events",
                column: "recruiter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_events_user_id",
                schema: "platform",
                table: "job_opening_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_recruiters_job_opening_id_user_id",
                schema: "platform",
                table: "job_opening_recruiters",
                columns: new[] { "job_opening_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_opening_recruiters_user_id",
                schema: "platform",
                table: "job_opening_recruiters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_job_opening_recruiters_lead",
                schema: "platform",
                table: "job_opening_recruiters",
                column: "job_opening_id",
                unique: true,
                filter: "is_lead");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_client_id_status",
                schema: "platform",
                table: "job_openings",
                columns: new[] { "client_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_document_template_id",
                schema: "platform",
                table: "job_openings",
                column: "document_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_education_level_id",
                schema: "platform",
                table: "job_openings",
                column: "education_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_lead_recruiter_id",
                schema: "platform",
                table: "job_openings",
                column: "lead_recruiter_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_legacy_code",
                schema: "platform",
                table: "job_openings",
                column: "legacy_code",
                unique: true,
                filter: "legacy_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_job_openings_zone_id",
                schema: "platform",
                table: "job_openings",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_comments_acting_for_user_id",
                schema: "platform",
                table: "opening_comments",
                column: "acting_for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_comments_author_user_id",
                schema: "platform",
                table: "opening_comments",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_comments_job_opening_id_created_at",
                schema: "platform",
                table: "opening_comments",
                columns: new[] { "job_opening_id", "created_at" });

            migrationBuilder.Sql(RecruitingSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecruitingSql.Down);

            migrationBuilder.DropTable(
                name: "candidate_notes",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "candidate_stage_history",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "interviews",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "job_opening_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "job_opening_recruiters",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "opening_comments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "candidates",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "job_openings",
                schema: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting,recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
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
        }
    }
}
