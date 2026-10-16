using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedCountAndMeasureAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "UnitOfMeasureAliases",
                columns: new[] { "Id", "Alias", "CreatedAt", "UnitOfMeasureId" },
                values: new object[,]
                {
                    { new Guid("a2000000-0000-0000-0000-000000000130"), "clove", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000131"), "cloves", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000132"), "slice", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000133"), "slices", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000134"), "bunch", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000135"), "bunches", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000136"), "stick", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2000000-0000-0000-0000-000000000137"), "sticks", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "BaseUnitOfMeasureId", "ConversionFactor", "Name", "UnitOfMeasureType" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000014"), "pinch", new Guid("a1000000-0000-0000-0000-000000000003"), 0.31m, "Pinch", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000015"), "dash", new Guid("a1000000-0000-0000-0000-000000000003"), 0.62m, "Dash", "Volume" }
                });

            migrationBuilder.InsertData(
                table: "UnitOfMeasureAliases",
                columns: new[] { "Id", "Alias", "CreatedAt", "UnitOfMeasureId" },
                values: new object[,]
                {
                    { new Guid("a2000000-0000-0000-0000-000000000140"), "pinches", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000014") },
                    { new Guid("a2000000-0000-0000-0000-000000000141"), "dashes", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000015") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000130"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000131"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000132"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000133"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000134"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000135"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000136"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000137"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000140"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000141"));

            migrationBuilder.DeleteData(
                table: "UnitsOfMeasure",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "UnitsOfMeasure",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000015"));
        }
    }
}
