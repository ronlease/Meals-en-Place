using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipesTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Title",
                table: "Recipes",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_Title",
                table: "Recipes");
        }
    }
}
