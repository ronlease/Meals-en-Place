using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnresolvedUomTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnresolvedUomTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SampleIngredientContext = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SampleMeasureString = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UnitToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnresolvedUomTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedUomTokens_UnitToken",
                table: "UnresolvedUomTokens",
                column: "UnitToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnresolvedUomTokens");
        }
    }
}
