using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Platform.Infrastructure.Persistence.MigrationSql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Platform.Core.People;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PeopleDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "people",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    first_names = table.Column<string>(type: "text", nullable: false),
                    last_name_paternal = table.Column<string>(type: "text", nullable: false),
                    last_name_maternal = table.Column<string>(type: "text", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: false, computedColumnSql: "last_name_paternal || ' ' || COALESCE(last_name_maternal || ' ', '') || first_names", stored: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    birth_state = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<Gender>(type: "platform.gender", nullable: false, defaultValue: Gender.Unspecified),
                    is_foreign = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    curp = table.Column<string>(type: "text", nullable: true),
                    rfc = table.Column<string>(type: "text", nullable: true),
                    nss = table.Column<string>(type: "text", nullable: true),
                    mobile_phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    ext_number = table.Column<string>(type: "text", nullable: true),
                    neighborhood = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    municipality = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<CandidateSource>(type: "platform.candidate_source", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_people", x => x.id);
                    table.UniqueConstraint("ak_people_uid", x => x.uid);
                    table.CheckConstraint("ck_people_curp", "curp ~ '^[A-Z]{4}\\d{6}[HM][A-Z]{5}[A-Z0-9]\\d$'");
                    table.CheckConstraint("ck_people_mobile_phone", "mobile_phone ~ '^\\d{10}$'");
                    table.CheckConstraint("ck_people_nss", "nss ~ '^\\d{11}$'");
                    table.CheckConstraint("ck_people_postal_code", "postal_code ~ '^\\d{5}$'");
                    table.CheckConstraint("ck_people_rfc", "rfc ~ '^[A-ZÑ&]{4}\\d{6}[A-Z0-9]{3}$'");
                });

            migrationBuilder.CreateTable(
                name: "person_documents",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<DocumentStatus>(type: "platform.document_status", nullable: false, defaultValue: DocumentStatus.Delivered),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    original_filename = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_documents", x => x.id);
                    table.UniqueConstraint("ak_person_documents_uid", x => x.uid);
                    table.CheckConstraint("ck_person_documents_dates", "expires_at IS NULL OR issued_at IS NULL OR expires_at >= issued_at");
                    table.CheckConstraint("ck_person_documents_rejection", "status <> 'rejected' OR rejection_reason IS NOT NULL");
                    table.CheckConstraint("ck_person_documents_size", "size_bytes > 0 AND size_bytes <= 10485760");
                    table.ForeignKey(
                        name: "fk_person_documents_document_types_document_type_id",
                        column: x => x.document_type_id,
                        principalSchema: "platform",
                        principalTable: "document_types",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_person_documents_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "platform",
                        principalTable: "people",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_person_documents_person_documents_superseded_by",
                        column: x => x.superseded_by,
                        principalSchema: "platform",
                        principalTable: "person_documents",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_person_documents_users_validated_by",
                        column: x => x.validated_by,
                        principalSchema: "platform",
                        principalTable: "users",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_people_curp",
                schema: "platform",
                table: "people",
                column: "curp",
                unique: true,
                filter: "curp IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_people_full_name_trgm",
                schema: "platform",
                table: "people",
                column: "full_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_people_mobile_phone",
                schema: "platform",
                table: "people",
                column: "mobile_phone");

            migrationBuilder.CreateIndex(
                name: "ix_people_rfc",
                schema: "platform",
                table: "people",
                column: "rfc",
                unique: true,
                filter: "rfc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_person_documents_document_type_id",
                schema: "platform",
                table: "person_documents",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_documents_expires_at",
                schema: "platform",
                table: "person_documents",
                column: "expires_at",
                filter: "superseded_by IS NULL AND expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_person_documents_storage_key",
                schema: "platform",
                table: "person_documents",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_documents_superseded_by",
                schema: "platform",
                table: "person_documents",
                column: "superseded_by");

            migrationBuilder.CreateIndex(
                name: "ix_person_documents_validated_by",
                schema: "platform",
                table: "person_documents",
                column: "validated_by");

            migrationBuilder.CreateIndex(
                name: "ux_person_documents_current",
                schema: "platform",
                table: "person_documents",
                columns: new[] { "person_id", "document_type_id" },
                unique: true,
                filter: "superseded_by IS NULL");

            migrationBuilder.Sql(PeopleSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PeopleSql.Down);

            migrationBuilder.DropTable(
                name: "person_documents",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "people",
                schema: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .Annotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .Annotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .Annotation("Npgsql:Enum:platform.permission_module", "recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .Annotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .Annotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:Enum:platform.audit_action", "insert,update,delete,unlock,export,login")
                .OldAnnotation("Npgsql:Enum:platform.candidate_source", "facebook,linkedin,whatsapp,referral,job_board,other")
                .OldAnnotation("Npgsql:Enum:platform.data_access_kind", "view_profile,view_document,download_document,decrypt_account")
                .OldAnnotation("Npgsql:Enum:platform.document_status", "pending,delivered,validated,rejected,expired")
                .OldAnnotation("Npgsql:Enum:platform.document_template_scope", "candidate,employee")
                .OldAnnotation("Npgsql:Enum:platform.gender", "male,female,unspecified")
                .OldAnnotation("Npgsql:Enum:platform.permission_module", "recruiting_reports,payroll_prepayroll,payroll_attendance,payroll_incidents,payroll_staff,payroll_payment_files,payroll_card_requests,users")
                .OldAnnotation("Npgsql:Enum:platform.registration_bank", "banorte,banregio")
                .OldAnnotation("Npgsql:Enum:platform.user_role", "administrator,recruiter,client")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
