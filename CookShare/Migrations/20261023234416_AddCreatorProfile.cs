using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorProfile : Migration
    {
        private static readonly string[] _slotColumns = { "UserId", "DisplayOrder" };
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "AspNetUsers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "AspNetUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfileFeaturedRecipes",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileFeaturedRecipes", x => new { x.UserId, x.RecipeId });
                    table.CheckConstraint("CK_ProfileFeaturedRecipes_DisplayOrder", "\"DisplayOrder\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ProfileFeaturedRecipes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileFeaturedRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileFeaturedRecipes_RecipeId",
                table: "ProfileFeaturedRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileFeaturedRecipes_UserId_DisplayOrder",
                table: "ProfileFeaturedRecipes",
                columns: _slotColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileFeaturedRecipes");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "AspNetUsers");
        }
    }
}
