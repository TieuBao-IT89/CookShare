using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCookingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CookingTimeMinutes",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Recipes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationTimeMinutes",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_CookingTimeMinutes",
                table: "Recipes",
                sql: "\"CookingTimeMinutes\" IS NULL OR \"CookingTimeMinutes\" BETWEEN 1 AND 1440");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_Difficulty",
                table: "Recipes",
                sql: "\"Difficulty\" IS NULL OR \"Difficulty\" IN ('Easy', 'Medium', 'Hard')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_PreparationTimeMinutes",
                table: "Recipes",
                sql: "\"PreparationTimeMinutes\" IS NULL OR \"PreparationTimeMinutes\" BETWEEN 1 AND 1440");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_CookingTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_Difficulty",
                table: "Recipes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_PreparationTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CookingTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PreparationTimeMinutes",
                table: "Recipes");
        }
    }
}
