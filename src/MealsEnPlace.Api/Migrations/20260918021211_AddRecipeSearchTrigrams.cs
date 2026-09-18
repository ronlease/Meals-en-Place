using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <summary>
    /// Enables the <c>pg_trgm</c> PostgreSQL extension and creates two GIN trigram
    /// indexes to support case-insensitive substring search (ILIKE) on recipe titles
    /// and canonical ingredient names at 1.6 M-row scale without sequential scans.
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>IX_Recipes_Title_Trgm</c> — GIN trigram index on <c>Recipes.Title</c>;
    ///     used by <c>GET /api/v1/recipes?q=...</c> title search.
    ///   </description></item>
    ///   <item><description>
    ///     <c>IX_CanonicalIngredients_Name_Trgm</c> — GIN trigram index on
    ///     <c>CanonicalIngredients.Name</c>; used by
    ///     <c>GET /api/v1/recipes?ingredient=...</c> ingredient-name search.
    ///   </description></item>
    /// </list>
    /// The existing B-tree <c>IX_Recipes_Title</c> is kept for <c>ORDER BY Title</c>
    /// performance; the GIN index is a separate structure optimised for ILIKE.
    /// <c>Down()</c> drops the two GIN indexes but does not drop the
    /// <c>pg_trgm</c> extension — other indexes or queries may depend on it.
    /// </summary>
    public partial class AddRecipeSearchTrigrams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Must be enabled before the GIN operator-class indexes can be created.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalIngredients_Name_Trgm",
                table: "CanonicalIngredients",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Title_Trgm",
                table: "Recipes",
                column: "Title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanonicalIngredients_Name_Trgm",
                table: "CanonicalIngredients");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_Title_Trgm",
                table: "Recipes");

            // pg_trgm extension is intentionally not dropped — other indexes or
            // queries in the database may depend on it.
        }
    }
}
