using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseInsensitiveNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_zones_state_name_es",
                schema: "platform",
                table: "zones");

            migrationBuilder.DropIndex(
                name: "ix_positions_catalog_name_es",
                schema: "platform",
                table: "positions_catalog");

            migrationBuilder.DropIndex(
                name: "ix_education_levels_name_es",
                schema: "platform",
                table: "education_levels");

            migrationBuilder.DropIndex(
                name: "ix_document_templates_name_es",
                schema: "platform",
                table: "document_templates");

            migrationBuilder.DropIndex(
                name: "ix_client_sites_client_id_name",
                schema: "platform",
                table: "client_sites");

            migrationBuilder.DropIndex(
                name: "ix_benefits_name_es",
                schema: "platform",
                table: "benefits");

            migrationBuilder.CreateIndex(
                name: "ix_client_sites_client_id",
                schema: "platform",
                table: "client_sites",
                column: "client_id");

            // D-021: names unique whatever their capitals («Planta Uno» / «planta uno»). EF has no expression indexes, so the
            // model leaves these out and the API's unique-violation handling answers «Ya existe…» as before. A database that
            // already holds two spellings stops here with the table named, to be merged by hand first.
            migrationBuilder.Sql("""
                DO $$
                DECLARE t text;
                BEGIN
                  FOR t IN
                    SELECT 'zones' FROM platform.zones GROUP BY state, lower(name_es) HAVING count(*) > 1
                    UNION ALL SELECT 'positions_catalog' FROM platform.positions_catalog GROUP BY lower(name_es) HAVING count(*) > 1
                    UNION ALL SELECT 'education_levels' FROM platform.education_levels GROUP BY lower(name_es) HAVING count(*) > 1
                    UNION ALL SELECT 'benefits' FROM platform.benefits GROUP BY lower(name_es) HAVING count(*) > 1
                    UNION ALL SELECT 'document_templates' FROM platform.document_templates GROUP BY lower(name_es) HAVING count(*) > 1
                    UNION ALL SELECT 'client_sites' FROM platform.client_sites GROUP BY client_id, lower(name) HAVING count(*) > 1
                  LOOP
                    RAISE EXCEPTION 'platform.% has names that differ only in capitals; merge them before this migration', t;
                  END LOOP;
                END $$;
                CREATE UNIQUE INDEX ux_zones_state_name ON platform.zones (state, lower(name_es));
                CREATE UNIQUE INDEX ux_positions_catalog_name ON platform.positions_catalog (lower(name_es));
                CREATE UNIQUE INDEX ux_education_levels_name ON platform.education_levels (lower(name_es));
                CREATE UNIQUE INDEX ux_benefits_name ON platform.benefits (lower(name_es));
                CREATE UNIQUE INDEX ux_document_templates_name ON platform.document_templates (lower(name_es));
                CREATE UNIQUE INDEX ux_client_sites_client_name ON platform.client_sites (client_id, lower(name));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX platform.ux_zones_state_name;
                DROP INDEX platform.ux_positions_catalog_name;
                DROP INDEX platform.ux_education_levels_name;
                DROP INDEX platform.ux_benefits_name;
                DROP INDEX platform.ux_document_templates_name;
                DROP INDEX platform.ux_client_sites_client_name;
                """);

            migrationBuilder.DropIndex(
                name: "ix_client_sites_client_id",
                schema: "platform",
                table: "client_sites");

            migrationBuilder.CreateIndex(
                name: "ix_zones_state_name_es",
                schema: "platform",
                table: "zones",
                columns: new[] { "state", "name_es" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_positions_catalog_name_es",
                schema: "platform",
                table: "positions_catalog",
                column: "name_es",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_education_levels_name_es",
                schema: "platform",
                table: "education_levels",
                column: "name_es",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_templates_name_es",
                schema: "platform",
                table: "document_templates",
                column: "name_es",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_sites_client_id_name",
                schema: "platform",
                table: "client_sites",
                columns: new[] { "client_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_benefits_name_es",
                schema: "platform",
                table: "benefits",
                column: "name_es",
                unique: true);
        }
    }
}
