using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalIngredientAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanonicalIngredientAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalIngredientAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanonicalIngredientAliases_CanonicalIngredients_CanonicalIn~",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalIngredientAliases_Alias",
                table: "CanonicalIngredientAliases",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalIngredientAliases_CanonicalIngredientId",
                table: "CanonicalIngredientAliases",
                column: "CanonicalIngredientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanonicalIngredientAliases");
        }
    }
}
