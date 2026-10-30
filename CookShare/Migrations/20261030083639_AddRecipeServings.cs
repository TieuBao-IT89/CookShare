using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeServings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Servings",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_Servings",
                table: "Recipes",
                sql: "\"Servings\" BETWEEN 1 AND 20");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_Servings",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "Recipes");
        }
    }
}
