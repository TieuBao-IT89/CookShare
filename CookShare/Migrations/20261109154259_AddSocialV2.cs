using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CookShare.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SearchIngredients",
                table: "Recipes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Recipes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CookedRecipePostId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSearchName",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterId = table.Column<string>(type: "text", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                    table.CheckConstraint("CK_ContentReports_TargetId", "\"TargetId\" > 0");
                    table.ForeignKey(
                        name: "FK_ContentReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CookedRecipePosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImageFileName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookedRecipePosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookedRecipePosts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookedRecipePosts_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeCollections_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    ModeratorId = table.Column<string>(type: "text", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationActions_AspNetUsers_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModerationActions_ContentReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ContentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionRecipes",
                columns: table => new
                {
                    CollectionId = table.Column<int>(type: "integer", nullable: false),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionRecipes", x => new { x.CollectionId, x.RecipeId });
                    table.ForeignKey(
                        name: "FK_CollectionRecipes_RecipeCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "RecipeCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CreatedAt_Id",
                table: "Recipes",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CookedRecipePostId",
                table: "Notifications",
                column: "CookedRecipePostId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionRecipes_CollectionId_AddedAt_RecipeId",
                table: "CollectionRecipes",
                columns: new[] { "CollectionId", "AddedAt", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionRecipes_RecipeId",
                table: "CollectionRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId",
                table: "ContentReports",
                columns: new[] { "ReporterId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_Status_CreatedAt_Id",
                table: "ContentReports",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CookedRecipePosts_CreatedAt_Id",
                table: "CookedRecipePosts",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CookedRecipePosts_RecipeId_CreatedAt_Id",
                table: "CookedRecipePosts",
                columns: new[] { "RecipeId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CookedRecipePosts_UserId_CreatedAt_Id",
                table: "CookedRecipePosts",
                columns: new[] { "UserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CookedRecipePosts_UserId_SubmissionKey",
                table: "CookedRecipePosts",
                columns: new[] { "UserId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ModeratorId",
                table: "ModerationActions",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ReportId_CreatedAt",
                table: "ModerationActions",
                columns: new[] { "ReportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCollections_OwnerId_UpdatedAt_Id",
                table: "RecipeCollections",
                columns: new[] { "OwnerId", "UpdatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_CookedRecipePosts_CookedRecipePostId",
                table: "Notifications",
                column: "CookedRecipePostId",
                principalTable: "CookedRecipePosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
            // Backfill existing recipes and public author names without changing their content.
            migrationBuilder.Sql("""
                UPDATE "Recipes" SET
                  "SearchText" = translate(regexp_replace(lower(normalize(COALESCE("Name", '') || ' ' || COALESCE("Description", ''), NFD)), '[̀-ͯ]', '', 'g'), 'đĐ', 'dd'),
                  "SearchIngredients" = translate(regexp_replace(lower(normalize(COALESCE("Ingredients", ''), NFD)), '[̀-ͯ]', '', 'g'), 'đĐ', 'dd');
                UPDATE "AspNetUsers" SET
                  "PublicSearchName" = translate(regexp_replace(lower(normalize(COALESCE("DisplayName", '') || ' ' || COALESCE("UserName", ''), NFD)), '[̀-ͯ]', '', 'g'), 'đĐ', 'dd');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_CookedRecipePosts_CookedRecipePostId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "CollectionRecipes");

            migrationBuilder.DropTable(
                name: "CookedRecipePosts");

            migrationBuilder.DropTable(
                name: "ModerationActions");

            migrationBuilder.DropTable(
                name: "RecipeCollections");

            migrationBuilder.DropTable(
                name: "ContentReports");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_CreatedAt_Id",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CookedRecipePostId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SearchIngredients",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CookedRecipePostId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PublicSearchName",
                table: "AspNetUsers");
        }
    }
}
