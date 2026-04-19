using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <summary>
    /// Removes the unique index on UnitOfMeasureAliases.Alias. Recipe notation
    /// uses case meaningfully -- uppercase "T" = Tablespoon, lowercase "t" =
    /// Teaspoon (a 3x quantity difference) -- so a database-level unique
    /// constraint (case-sensitive or case-insensitive) fails to express the
    /// real domain. Uniqueness is enforced instead at the service / controller
    /// layer, which can permit legitimate case-sensitive variants while still
    /// rejecting accidental duplicates.
    /// </summary>
    public partial class DropAliasUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasureAliases_Alias",
                table: "UnitOfMeasureAliases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureAliases_Alias",
                table: "UnitOfMeasureAliases",
                column: "Alias",
                unique: true);
        }
    }
}
