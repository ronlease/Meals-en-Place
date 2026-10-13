using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedGallonUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "BaseUnitOfMeasureId", "ConversionFactor", "Name", "UnitOfMeasureType" },
                values: new object[] { new Guid("a1000000-0000-0000-0000-000000000016"), "gal", new Guid("a1000000-0000-0000-0000-000000000003"), 3785.41m, "Gallon", "Volume" });

            migrationBuilder.InsertData(
                table: "UnitOfMeasureAliases",
                columns: new[] { "Id", "Alias", "CreatedAt", "UnitOfMeasureId" },
                values: new object[,]
                {
                    { new Guid("a2000000-0000-0000-0000-000000000150"), "gal.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000016") },
                    { new Guid("a2000000-0000-0000-0000-000000000151"), "gals", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000016") },
                    { new Guid("a2000000-0000-0000-0000-000000000152"), "gals.", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000016") },
                    { new Guid("a2000000-0000-0000-0000-000000000153"), "gallon", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000016") },
                    { new Guid("a2000000-0000-0000-0000-000000000154"), "gallons", new DateTime(2026, 4, 19, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("a1000000-0000-0000-0000-000000000016") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000150"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000151"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000152"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000153"));

            migrationBuilder.DeleteData(
                table: "UnitOfMeasureAliases",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000154"));

            migrationBuilder.DeleteData(
                table: "UnitsOfMeasure",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000016"));
        }
    }
}
