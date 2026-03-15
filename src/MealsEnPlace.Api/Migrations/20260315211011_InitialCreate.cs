using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MealsEnPlace.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuisineType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    ServingCount = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TheMealDbId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseUomId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UomType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitsOfMeasure_UnitsOfMeasure_BaseUomId",
                        column: x => x.BaseUomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplaySystem = table.Column<string>(type: "text", nullable: false, defaultValue: "Imperial")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.CheckConstraint("CK_UserPreferences_SingleRow", "\"Id\" = 'd1000000-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "MealPlanSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "text", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealSlot = table.Column<string>(type: "text", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanSlots_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealPlanSlots_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeDietaryTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDietaryTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeDietaryTags_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DefaultUomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanonicalIngredients_UnitsOfMeasure_DefaultUomId",
                        column: x => x.DefaultUomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_CanonicalIngredients_CanonicalIngredientId",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_UnitsOfMeasure_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsContainerResolved = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_CanonicalIngredients_CanonicalIngredientId",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_UnitsOfMeasure_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeasonalityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeakSeasonEnd = table.Column<string>(type: "text", nullable: false),
                    PeakSeasonStart = table.Column<string>(type: "text", nullable: false),
                    UsdaZone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "7a")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonalityWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonalityWindows_CanonicalIngredients_CanonicalIngredient~",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_CanonicalIngredients_CanonicalIngredientId",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "CanonicalIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_UnitsOfMeasure_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WasteAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedRecipeIds = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteAlerts_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "BaseUomId", "ConversionFactor", "Name", "UomType" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "ea", null, 1.0m, "Each", "Count" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "g", null, 1.0m, "Gram", "Weight" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "ml", null, 1.0m, "Milliliter", "Volume" }
                });

            migrationBuilder.InsertData(
                table: "UserPreferences",
                column: "Id",
                value: new Guid("d1000000-0000-0000-0000-000000000001"));

            migrationBuilder.InsertData(
                table: "CanonicalIngredients",
                columns: new[] { "Id", "Category", "DefaultUomId", "Name" },
                values: new object[,]
                {
                    { new Guid("b1000000-0000-0000-0000-000000000001"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Apples" },
                    { new Guid("b1000000-0000-0000-0000-000000000002"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Asparagus" },
                    { new Guid("b1000000-0000-0000-0000-000000000003"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Broccoli" },
                    { new Guid("b1000000-0000-0000-0000-000000000004"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Corn" },
                    { new Guid("b1000000-0000-0000-0000-000000000005"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Kale" },
                    { new Guid("b1000000-0000-0000-0000-000000000006"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Peaches" },
                    { new Guid("b1000000-0000-0000-0000-000000000007"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Pumpkin" },
                    { new Guid("b1000000-0000-0000-0000-000000000008"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Strawberries" },
                    { new Guid("b1000000-0000-0000-0000-000000000009"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Tomatoes" },
                    { new Guid("b1000000-0000-0000-0000-000000000010"), "Produce", new Guid("a1000000-0000-0000-0000-000000000001"), "Zucchini" }
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "Abbreviation", "BaseUomId", "ConversionFactor", "Name", "UomType" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "cup", new Guid("a1000000-0000-0000-0000-000000000003"), 236.588m, "Cup", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "fl oz", new Guid("a1000000-0000-0000-0000-000000000003"), 29.574m, "Fluid Ounce", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "L", new Guid("a1000000-0000-0000-0000-000000000003"), 1000.0m, "Liter", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), "pt", new Guid("a1000000-0000-0000-0000-000000000003"), 473.176m, "Pint", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000008"), "qt", new Guid("a1000000-0000-0000-0000-000000000003"), 946.353m, "Quart", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000009"), "tbsp", new Guid("a1000000-0000-0000-0000-000000000003"), 14.787m, "Tablespoon", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000010"), "tsp", new Guid("a1000000-0000-0000-0000-000000000003"), 4.929m, "Teaspoon", "Volume" },
                    { new Guid("a1000000-0000-0000-0000-000000000011"), "kg", new Guid("a1000000-0000-0000-0000-000000000002"), 1000.0m, "Kilogram", "Weight" },
                    { new Guid("a1000000-0000-0000-0000-000000000012"), "lb", new Guid("a1000000-0000-0000-0000-000000000002"), 453.592m, "Pound", "Weight" },
                    { new Guid("a1000000-0000-0000-0000-000000000013"), "oz", new Guid("a1000000-0000-0000-0000-000000000002"), 28.350m, "Ounce", "Weight" }
                });

            migrationBuilder.InsertData(
                table: "SeasonalityWindows",
                columns: new[] { "Id", "CanonicalIngredientId", "PeakSeasonEnd", "PeakSeasonStart", "UsdaZone" },
                values: new object[,]
                {
                    { new Guid("c1000000-0000-0000-0000-000000000001"), new Guid("b1000000-0000-0000-0000-000000000009"), "September", "June", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000002"), new Guid("b1000000-0000-0000-0000-000000000004"), "September", "July", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000003"), new Guid("b1000000-0000-0000-0000-000000000010"), "August", "June", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000004"), new Guid("b1000000-0000-0000-0000-000000000008"), "June", "May", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000005"), new Guid("b1000000-0000-0000-0000-000000000001"), "November", "September", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000006"), new Guid("b1000000-0000-0000-0000-000000000005"), "May", "March", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000007"), new Guid("b1000000-0000-0000-0000-000000000005"), "November", "September", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000008"), new Guid("b1000000-0000-0000-0000-000000000002"), "May", "April", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000009"), new Guid("b1000000-0000-0000-0000-000000000006"), "August", "July", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000010"), new Guid("b1000000-0000-0000-0000-000000000007"), "October", "September", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000011"), new Guid("b1000000-0000-0000-0000-000000000003"), "May", "April", "7a" },
                    { new Guid("c1000000-0000-0000-0000-000000000012"), new Guid("b1000000-0000-0000-0000-000000000003"), "October", "September", "7a" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalIngredients_DefaultUomId",
                table: "CanonicalIngredients",
                column: "DefaultUomId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalIngredients_Name",
                table: "CanonicalIngredients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CanonicalIngredientId",
                table: "InventoryItems",
                column: "CanonicalIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UomId",
                table: "InventoryItems",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSlots_MealPlanId_DayOfWeek_MealSlot",
                table: "MealPlanSlots",
                columns: new[] { "MealPlanId", "DayOfWeek", "MealSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSlots_RecipeId",
                table: "MealPlanSlots",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDietaryTags_RecipeId_Tag",
                table: "RecipeDietaryTags",
                columns: new[] { "RecipeId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_CanonicalIngredientId",
                table: "RecipeIngredients",
                column: "CanonicalIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_UomId",
                table: "RecipeIngredients",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TheMealDbId",
                table: "Recipes",
                column: "TheMealDbId",
                unique: true,
                filter: "\"TheMealDbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonalityWindows_CanonicalIngredientId",
                table: "SeasonalityWindows",
                column: "CanonicalIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_CanonicalIngredientId",
                table: "ShoppingListItems",
                column: "CanonicalIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_MealPlanId",
                table: "ShoppingListItems",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_UomId",
                table: "ShoppingListItems",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Abbreviation",
                table: "UnitsOfMeasure",
                column: "Abbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_BaseUomId",
                table: "UnitsOfMeasure",
                column: "BaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteAlerts_InventoryItemId",
                table: "WasteAlerts",
                column: "InventoryItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealPlanSlots");

            migrationBuilder.DropTable(
                name: "RecipeDietaryTags");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "SeasonalityWindows");

            migrationBuilder.DropTable(
                name: "ShoppingListItems");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "WasteAlerts");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "MealPlans");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "CanonicalIngredients");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");
        }
    }
}
