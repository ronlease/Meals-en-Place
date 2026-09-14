using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <summary>
    /// Adds the <c>RecipeReferenceCount</c> column to <c>CanonicalIngredients</c>.
    /// The column is NOT NULL DEFAULT 0.  After adding the column, a single UPDATE
    /// backfills the counts from <c>RecipeIngredients</c> so the denormalized value
    /// is correct immediately after the migration runs on an existing database.
    /// The ingest tool runs the same backfill at the end of a bulk ingest; interactive
    /// recipe creation via <c>RecipeImportService</c> increments the count per write.
    /// <c>Down()</c> drops the column.
    /// </summary>
    public partial class AddCanonicalIngredientRecipeReferenceCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecipeReferenceCount",
                table: "CanonicalIngredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill counts for the existing dataset so the column is accurate
            // immediately after the migration.  COUNT(*) returns bigint in PostgreSQL;
            // the ::integer cast aligns with the column type.
            migrationBuilder.Sql(
                """
                UPDATE "CanonicalIngredients" c
                SET "RecipeReferenceCount" = s.cnt
                FROM (
                    SELECT "CanonicalIngredientId", COUNT(*)::integer AS cnt
                    FROM "RecipeIngredients"
                    GROUP BY "CanonicalIngredientId"
                ) s
                WHERE s."CanonicalIngredientId" = c."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipeReferenceCount",
                table: "CanonicalIngredients");
        }
    }
}
