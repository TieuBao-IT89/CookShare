using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SearchIngredients",
                table: "Recipes",
                column: "SearchIngredients")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SearchText",
                table: "Recipes",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PublicSearchName",
                table: "AspNetUsers",
                column: "PublicSearchName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_SearchIngredients",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_SearchText",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PublicSearchName",
                table: "AspNetUsers");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
