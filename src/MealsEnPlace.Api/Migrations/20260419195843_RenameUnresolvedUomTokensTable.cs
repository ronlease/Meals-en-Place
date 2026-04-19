using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <summary>
    /// Renames the <c>UnresolvedUomTokens</c> table (plus its primary key and
    /// index) to their spelled-out form <c>UnresolvedUnitOfMeasureTokens</c>.
    /// Data-preserving: uses PostgreSQL RENAME operations rather than drop-
    /// and-recreate so any queue rows captured by prior ingest runs survive
    /// the migration.
    /// <para>
    /// EF Core's default scaffolding produced a DropTable + CreateTable pair.
    /// This hand-rolled version replaces that with RenameTable + SQL to rename
    /// the primary-key and index constraints so no rows are lost.
    /// </para>
    /// </summary>
    public partial class RenameUnresolvedUomTokensTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UnresolvedUomTokens",
                newName: "UnresolvedUnitOfMeasureTokens");

            migrationBuilder.Sql(
                """
                ALTER TABLE "UnresolvedUnitOfMeasureTokens"
                    RENAME CONSTRAINT "PK_UnresolvedUomTokens"
                    TO "PK_UnresolvedUnitOfMeasureTokens";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_UnresolvedUomTokens_UnitToken",
                table: "UnresolvedUnitOfMeasureTokens",
                newName: "IX_UnresolvedUnitOfMeasureTokens_UnitToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_UnresolvedUnitOfMeasureTokens_UnitToken",
                table: "UnresolvedUnitOfMeasureTokens",
                newName: "IX_UnresolvedUomTokens_UnitToken");

            migrationBuilder.Sql(
                """
                ALTER TABLE "UnresolvedUnitOfMeasureTokens"
                    RENAME CONSTRAINT "PK_UnresolvedUnitOfMeasureTokens"
                    TO "PK_UnresolvedUomTokens";
                """);

            migrationBuilder.RenameTable(
                name: "UnresolvedUnitOfMeasureTokens",
                newName: "UnresolvedUomTokens");
        }
    }
}
