using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameUomColumnsToUnitOfMeasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanonicalIngredients_UnitsOfMeasure_DefaultUomId",
                table: "CanonicalIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UomId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_UnitsOfMeasure_UomId",
                table: "RecipeIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_UnitsOfMeasure_UomId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_UnitsOfMeasure_BaseUomId",
                table: "UnitsOfMeasure");

            migrationBuilder.RenameColumn(
                name: "UomType",
                table: "UnitsOfMeasure",
                newName: "UnitOfMeasureType");

            migrationBuilder.RenameColumn(
                name: "BaseUomId",
                table: "UnitsOfMeasure",
                newName: "BaseUnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitsOfMeasure_BaseUomId",
                table: "UnitsOfMeasure",
                newName: "IX_UnitsOfMeasure_BaseUnitOfMeasureId");

            migrationBuilder.RenameColumn(
                name: "UomId",
                table: "ShoppingListItems",
                newName: "UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_UomId",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_UnitOfMeasureId");

            migrationBuilder.RenameColumn(
                name: "UomId",
                table: "RecipeIngredients",
                newName: "UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_UomId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_UnitOfMeasureId");

            migrationBuilder.RenameColumn(
                name: "UomId",
                table: "InventoryItems",
                newName: "UnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_UomId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_UnitOfMeasureId");

            migrationBuilder.RenameColumn(
                name: "DefaultUomId",
                table: "CanonicalIngredients",
                newName: "DefaultUnitOfMeasureId");

            migrationBuilder.RenameIndex(
                name: "IX_CanonicalIngredients_DefaultUomId",
                table: "CanonicalIngredients",
                newName: "IX_CanonicalIngredients_DefaultUnitOfMeasureId");

            migrationBuilder.AddForeignKey(
                name: "FK_CanonicalIngredients_UnitsOfMeasure_DefaultUnitOfMeasureId",
                table: "CanonicalIngredients",
                column: "DefaultUnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UnitOfMeasureId",
                table: "InventoryItems",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_UnitsOfMeasure_UnitOfMeasureId",
                table: "RecipeIngredients",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_UnitsOfMeasure_UnitOfMeasureId",
                table: "ShoppingListItems",
                column: "UnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_UnitsOfMeasure_BaseUnitOfMeasureId",
                table: "UnitsOfMeasure",
                column: "BaseUnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanonicalIngredients_UnitsOfMeasure_DefaultUnitOfMeasureId",
                table: "CanonicalIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UnitOfMeasureId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_UnitsOfMeasure_UnitOfMeasureId",
                table: "RecipeIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_UnitsOfMeasure_UnitOfMeasureId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_UnitsOfMeasure_BaseUnitOfMeasureId",
                table: "UnitsOfMeasure");

            migrationBuilder.RenameColumn(
                name: "UnitOfMeasureType",
                table: "UnitsOfMeasure",
                newName: "UomType");

            migrationBuilder.RenameColumn(
                name: "BaseUnitOfMeasureId",
                table: "UnitsOfMeasure",
                newName: "BaseUomId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitsOfMeasure_BaseUnitOfMeasureId",
                table: "UnitsOfMeasure",
                newName: "IX_UnitsOfMeasure_BaseUomId");

            migrationBuilder.RenameColumn(
                name: "UnitOfMeasureId",
                table: "ShoppingListItems",
                newName: "UomId");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_UnitOfMeasureId",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_UomId");

            migrationBuilder.RenameColumn(
                name: "UnitOfMeasureId",
                table: "RecipeIngredients",
                newName: "UomId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_UnitOfMeasureId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_UomId");

            migrationBuilder.RenameColumn(
                name: "UnitOfMeasureId",
                table: "InventoryItems",
                newName: "UomId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_UnitOfMeasureId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_UomId");

            migrationBuilder.RenameColumn(
                name: "DefaultUnitOfMeasureId",
                table: "CanonicalIngredients",
                newName: "DefaultUomId");

            migrationBuilder.RenameIndex(
                name: "IX_CanonicalIngredients_DefaultUnitOfMeasureId",
                table: "CanonicalIngredients",
                newName: "IX_CanonicalIngredients_DefaultUomId");

            migrationBuilder.AddForeignKey(
                name: "FK_CanonicalIngredients_UnitsOfMeasure_DefaultUomId",
                table: "CanonicalIngredients",
                column: "DefaultUomId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UomId",
                table: "InventoryItems",
                column: "UomId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_UnitsOfMeasure_UomId",
                table: "RecipeIngredients",
                column: "UomId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_UnitsOfMeasure_UomId",
                table: "ShoppingListItems",
                column: "UomId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_UnitsOfMeasure_BaseUomId",
                table: "UnitsOfMeasure",
                column: "BaseUomId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
