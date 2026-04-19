using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitOfMeasureAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitOfMeasureAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasureAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureAliases_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "UnitOfMeasureAliases",
                columns: new[] { "Id", "Alias", "CreatedAt", "UnitOfMeasureId" },
                values: new object[,]
                {
                    { new Guid("a2000000-0000-0000-0000-000000000001"), "c", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("a2000000-0000-0000-0000-000000000002"), "c.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("a2000000-0000-0000-0000-000000000003"), "cups", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("a2000000-0000-0000-0000-000000000010"), "T", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000011"), "T.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000012"), "Tbs", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000013"), "Tbs.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000014"), "Tbl", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000015"), "Tbsp.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000016"), "tbsps", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000017"), "tablespoons", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000009") },
                    { new Guid("a2000000-0000-0000-0000-000000000020"), "t", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000010") },
                    { new Guid("a2000000-0000-0000-0000-000000000021"), "t.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000010") },
                    { new Guid("a2000000-0000-0000-0000-000000000022"), "tsp.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000010") },
                    { new Guid("a2000000-0000-0000-0000-000000000023"), "tsps", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000010") },
                    { new Guid("a2000000-0000-0000-0000-000000000024"), "teaspoons", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000010") },
                    { new Guid("a2000000-0000-0000-0000-000000000030"), "oz.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000013") },
                    { new Guid("a2000000-0000-0000-0000-000000000031"), "ozs", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000013") },
                    { new Guid("a2000000-0000-0000-0000-000000000032"), "ozs.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000013") },
                    { new Guid("a2000000-0000-0000-0000-000000000033"), "ounces", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000013") },
                    { new Guid("a2000000-0000-0000-0000-000000000040"), "fl. oz", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("a2000000-0000-0000-0000-000000000041"), "fl. oz.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("a2000000-0000-0000-0000-000000000042"), "fluid oz", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("a2000000-0000-0000-0000-000000000043"), "fluid ounces", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("a2000000-0000-0000-0000-000000000050"), "lb.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000012") },
                    { new Guid("a2000000-0000-0000-0000-000000000051"), "lbs", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000012") },
                    { new Guid("a2000000-0000-0000-0000-000000000052"), "lbs.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000012") },
                    { new Guid("a2000000-0000-0000-0000-000000000053"), "pounds", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000012") },
                    { new Guid("a2000000-0000-0000-0000-000000000060"), "g.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("a2000000-0000-0000-0000-000000000061"), "gm", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("a2000000-0000-0000-0000-000000000062"), "gms", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("a2000000-0000-0000-0000-000000000063"), "grams", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("a2000000-0000-0000-0000-000000000070"), "kg.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000011") },
                    { new Guid("a2000000-0000-0000-0000-000000000071"), "kgs", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000011") },
                    { new Guid("a2000000-0000-0000-0000-000000000072"), "kilograms", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000011") },
                    { new Guid("a2000000-0000-0000-0000-000000000080"), "ml.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("a2000000-0000-0000-0000-000000000081"), "mls", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("a2000000-0000-0000-0000-000000000082"), "milliliters", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("a2000000-0000-0000-0000-000000000090"), "l", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("a2000000-0000-0000-0000-000000000091"), "l.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("a2000000-0000-0000-0000-000000000092"), "liters", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("a2000000-0000-0000-0000-000000000093"), "litres", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("a2000000-0000-0000-0000-000000000100"), "pt.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("a2000000-0000-0000-0000-000000000101"), "pts", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("a2000000-0000-0000-0000-000000000102"), "pints", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("a2000000-0000-0000-0000-000000000110"), "qt.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000008") },
                    { new Guid("a2000000-0000-0000-0000-000000000111"), "qts", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000008") },
                    { new Guid("a2000000-0000-0000-0000-000000000112"), "quarts", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000008") },
                    { new Guid("a2000000-0000-0000-0000-000000000120"), "each", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureAliases_Alias",
                table: "UnitOfMeasureAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureAliases_UnitOfMeasureId",
                table: "UnitOfMeasureAliases",
                column: "UnitOfMeasureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitOfMeasureAliases");
        }
    }
}
