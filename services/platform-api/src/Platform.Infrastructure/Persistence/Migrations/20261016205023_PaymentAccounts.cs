using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Core.People;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Uid keys named after the table like every other (EntityConventions); renamed in place, so the foreign keys stay.
            migrationBuilder.Sql("ALTER TABLE platform.job_openings RENAME CONSTRAINT ak_job_opening_uid TO ak_job_openings_uid;");
            migrationBuilder.Sql("ALTER TABLE platform.document_types RENAME CONSTRAINT ak_document_type_uid TO ak_document_types_uid;");

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

            migrationBuilder.CreateTable(
                name: "person_bank_accounts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_type = table.Column<BankAccountType>(type: "platform.bank_account_type", nullable: false),
                    account_number_enc = table.Column<byte[]>(type: "bytea", nullable: false),
                    account_last4 = table.Column<string>(type: "text", nullable: false),
                    holder_name = table.Column<string>(type: "text", nullable: false),
                    alias = table.Column<string>(type: "text", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_frequent_account_registered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_bank_accounts", x => x.id);
                    table.UniqueConstraint("ak_person_bank_accounts_uid", x => x.uid);
                    table.CheckConstraint("ck_person_bank_accounts_holder", "length(holder_name) BETWEEN 1 AND 40");
                    table.CheckConstraint("ck_person_bank_accounts_last4", "account_last4 ~ '^\\d{4}$'");
                    table.CheckConstraint("ck_person_bank_accounts_primary_current", "NOT is_primary OR valid_to IS NULL");
                    table.CheckConstraint("ck_person_bank_accounts_validity", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_person_bank_accounts_banks_bank_id",
                        column: x => x.bank_id,
                        principalSchema: "platform",
                        principalTable: "banks",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_person_bank_accounts_person_person_id",
                        column: x => x.person_id,
                        principalSchema: "platform",
                        principalTable: "people",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_bank_accounts_bank_id",
                schema: "platform",
                table: "person_bank_accounts",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "ux_person_bank_accounts_primary",
                schema: "platform",
                table: "person_bank_accounts",
                column: "person_id",
                unique: true,
                filter: "is_primary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE platform.job_openings RENAME CONSTRAINT ak_job_openings_uid TO ak_job_opening_uid;");
            migrationBuilder.Sql("ALTER TABLE platform.document_types RENAME CONSTRAINT ak_document_types_uid TO ak_document_type_uid;");

            migrationBuilder.DropTable(
                name: "person_bank_accounts",
                schema: "platform");

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
        }
    }
}
