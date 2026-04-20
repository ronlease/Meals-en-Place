using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealConsumptionAndAutoDepleteAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoDepleteOnConsume",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedAt",
                table: "MealPlanSlots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConsumedWithAutoDeplete",
                table: "MealPlanSlots",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConsumeAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeductedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MealPlanSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalLocation = table.Column<string>(type: "text", nullable: false),
                    OriginalInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumeAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumeAuditEntries_CanonicalIngredients_CanonicalIngredien~",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumeAuditEntries_MealPlanSlots_MealPlanSlotId",
                        column: x => x.MealPlanSlotId,
                        principalTable: "MealPlanSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsumeAuditEntries_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "UserPreferences",
                keyColumn: "Id",
                keyValue: new Guid("d1000000-0000-0000-0000-000000000001"),
                column: "AutoDepleteOnConsume",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumeAuditEntries_CanonicalIngredientId",
                table: "ConsumeAuditEntries",
                column: "CanonicalIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumeAuditEntries_MealPlanSlotId",
                table: "ConsumeAuditEntries",
                column: "MealPlanSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumeAuditEntries_UnitOfMeasureId",
                table: "ConsumeAuditEntries",
                column: "UnitOfMeasureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumeAuditEntries");

            migrationBuilder.DropColumn(
                name: "AutoDepleteOnConsume",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                table: "MealPlanSlots");

            migrationBuilder.DropColumn(
                name: "ConsumedWithAutoDeplete",
                table: "MealPlanSlots");
        }
    }
}
