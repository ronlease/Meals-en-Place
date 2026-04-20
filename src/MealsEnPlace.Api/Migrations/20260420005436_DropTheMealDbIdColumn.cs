using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropTheMealDbIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_TheMealDbId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "TheMealDbId",
                table: "Recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TheMealDbId",
                table: "Recipes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TheMealDbId",
                table: "Recipes",
                column: "TheMealDbId",
                unique: true,
                filter: "\"TheMealDbId\" IS NOT NULL");
        }
    }
}
