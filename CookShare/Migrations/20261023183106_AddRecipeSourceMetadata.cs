using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeSourceMetadata : Migration
    {
        private static readonly string[] _sourceIdentityColumns = { "Source", "ExternalId" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Recipes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Recipes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Recipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Source_ExternalId",
                table: "Recipes",
                columns: _sourceIdentityColumns,
                unique: true,
                filter: "\"Source\" IS NOT NULL AND \"ExternalId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_SourceIdentity",
                table: "Recipes",
                sql: "(\"Source\" IS NULL AND \"ExternalId\" IS NULL) OR (\"Source\" IS NOT NULL AND \"ExternalId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_Source_ExternalId",
                table: "Recipes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_SourceIdentity",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Recipes");
        }
    }
}
