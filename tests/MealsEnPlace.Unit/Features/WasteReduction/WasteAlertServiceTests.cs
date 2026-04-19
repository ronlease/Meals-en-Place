// Feature: Waste Reduction Alerts
//
// Scenario: Alert for an expiry-imminent item with a matching recipe
//   Given I have "Greek Yogurt" in my fridge expiring in 2 days
//   And a recipe "Yogurt Parfait" uses "Greek Yogurt"
//   And "Yogurt Parfait" is fully resolved
//   When the system evaluates waste alerts
//   Then a WasteAlert is surfaced for "Greek Yogurt"
//   And the alert references "Yogurt Parfait" as a suggested recipe
//
// Scenario: No alert when no recipe matches the expiring item
//   Given I have "Specialty Sauce" in my fridge expiring in 1 day
//   And no recipe in my library uses "Specialty Sauce"
//   When the system evaluates waste alerts
//   Then no WasteAlert is surfaced for "Specialty Sauce"
//
// Scenario: No alert for items not near expiry
//   Given I have "Butter" in my fridge expiring in 30 days
//   When the system evaluates waste alerts
//   Then no WasteAlert is surfaced for "Butter"
//
// Scenario: Multiple recipes suggested for one expiring item
//   Given I have "Heavy Cream" in my fridge expiring in 3 days
//   And recipes "Alfredo Pasta" and "Cream of Mushroom Soup" both use "Heavy Cream"
//   And both recipes are fully resolved
//   When the system evaluates waste alerts
//   Then a WasteAlert is surfaced for "Heavy Cream"
//   And the alert references both "Alfredo Pasta" and "Cream of Mushroom Soup"
//
// Scenario: No alert for items without an expiry date
//   Given I have "Salt" in my pantry with no expiry date set
//   When the system evaluates waste alerts
//   Then no WasteAlert is surfaced for "Salt"
//
// Scenario: Dismiss marks alert as dismissed and excludes from active list
//   Given an active waste alert exists
//   When I dismiss the alert
//   Then the alert no longer appears in active alerts
//
// Scenario: Unresolved recipes excluded from matched recipes
//   Given I have "Tomatoes" in my fridge expiring in 2 days
//   And a recipe "Salsa" uses "Tomatoes" but has an unresolved container reference
//   When the system evaluates waste alerts
//   Then no WasteAlert is surfaced for "Tomatoes"
//
// Scenario: No duplicate alert for same inventory item if active alert already exists
//   Given an active alert already exists for "Greek Yogurt"
//   When the system evaluates waste alerts again
//   Then only one alert exists for "Greek Yogurt"

using FluentAssertions;
using MealsEnPlace.Api.Features.WasteReduction;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.WasteReduction;

public class WasteAlertServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly WasteAlertService _sut;

    private static readonly Guid EachId = UnitOfMeasureConfiguration.EachId;
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;
    private static readonly Guid MlId = UnitOfMeasureConfiguration.MlId;

    public WasteAlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);
        SeedUnitOfMeasures();
        _sut = new WasteAlertService(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedUnitOfMeasures()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = EachId,
                Name = "Each",
                UnitOfMeasureType = UnitOfMeasureType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = GramId,
                Name = "Gram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = MlId,
                Name = "Milliliter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedCanonicalIngredient(string name)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = EachId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private InventoryItem SeedInventoryItem(
        Guid canonicalIngredientId,
        decimal quantity,
        Guid unitOfMeasureId,
        DateOnly? expiryDate = null,
        StorageLocation location = StorageLocation.Fridge)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = location,
            Quantity = quantity,
            UnitOfMeasureId = unitOfMeasureId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    private Recipe SeedFullyResolvedRecipe(
        string title,
        string cuisineType,
        IEnumerable<(Guid IngredientId, decimal Quantity, Guid UnitOfMeasureId)> ingredientLines)
    {
        var recipe = new Recipe
        {
            CuisineType = cuisineType,
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = title
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        foreach (var (ingredientId, qty, unitOfMeasureId) in ingredientLines)
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = qty,
                RecipeId = recipe.Id,
                UnitOfMeasureId = unitOfMeasureId
            });
        }
        _dbContext.SaveChanges();
        return recipe;
    }

    private Recipe SeedUnresolvedRecipe(
        string title,
        Guid ingredientId,
        decimal quantity,
        Guid unitOfMeasureId)
    {
        var recipe = new Recipe
        {
            CuisineType = "Test",
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = title
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = ingredientId,
            Id = Guid.NewGuid(),
            IsContainerResolved = false,
            Quantity = quantity,
            RecipeId = recipe.Id,
            UnitOfMeasureId = unitOfMeasureId
        });
        _dbContext.SaveChanges();
        return recipe;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAlertsAsync_ExpiryImminentWithMatchingRecipe_SurfacesAlert()
    {
        // Arrange
        var yogurt = SeedCanonicalIngredient("Greek Yogurt");
        SeedInventoryItem(yogurt.Id, 500m, GramId, Today().AddDays(2));
        var recipe = SeedFullyResolvedRecipe("Yogurt Parfait", "American",
            [(yogurt.Id, 200m, GramId)]);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().ContainSingle(a => a.CanonicalIngredientName == "Greek Yogurt");
        var alert = alerts.Single();
        alert.MatchedRecipes.Should().ContainSingle(r => r.RecipeId == recipe.Id);
        alert.MatchedRecipes[0].Title.Should().Be("Yogurt Parfait");
    }

    [Fact]
    public async Task EvaluateAlertsAsync_ExpiryImminentNoMatchingRecipe_NoAlert()
    {
        // Arrange
        var sauce = SeedCanonicalIngredient("Specialty Sauce");
        SeedInventoryItem(sauce.Id, 200m, MlId, Today().AddDays(1));

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_ItemNotNearExpiry_NoAlert()
    {
        // Arrange
        var butter = SeedCanonicalIngredient("Butter");
        SeedInventoryItem(butter.Id, 250m, GramId, Today().AddDays(30));
        SeedFullyResolvedRecipe("Butter Cookies", "American",
            [(butter.Id, 100m, GramId)]);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_MultipleRecipesForExpiringItem_AlertReferencesAll()
    {
        // Arrange
        var cream = SeedCanonicalIngredient("Heavy Cream");
        SeedInventoryItem(cream.Id, 500m, MlId, Today().AddDays(3));
        var alfredo = SeedFullyResolvedRecipe("Alfredo Pasta", "Italian",
            [(cream.Id, 200m, MlId)]);
        var soup = SeedFullyResolvedRecipe("Cream of Mushroom Soup", "American",
            [(cream.Id, 300m, MlId)]);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().ContainSingle(a => a.CanonicalIngredientName == "Heavy Cream");
        var alert = alerts.Single();
        alert.MatchedRecipes.Should().HaveCount(2);
        alert.MatchedRecipes.Select(r => r.RecipeId).Should()
            .Contain(alfredo.Id).And.Contain(soup.Id);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_ItemWithoutExpiryDate_NoAlert()
    {
        // Arrange
        var salt = SeedCanonicalIngredient("Salt");
        SeedInventoryItem(salt.Id, 500m, GramId, expiryDate: null, StorageLocation.Pantry);
        SeedFullyResolvedRecipe("Salty Dish", "American",
            [(salt.Id, 10m, GramId)]);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DismissAlertAsync_ActiveAlert_ExcludedFromActiveAlerts()
    {
        // Arrange
        var yogurt = SeedCanonicalIngredient("Greek Yogurt");
        SeedInventoryItem(yogurt.Id, 500m, GramId, Today().AddDays(2));
        SeedFullyResolvedRecipe("Yogurt Parfait", "American",
            [(yogurt.Id, 200m, GramId)]);

        var alerts = await _sut.EvaluateAlertsAsync();
        alerts.Should().HaveCount(1);

        // Act
        var result = await _sut.DismissAlertAsync(alerts[0].AlertId);

        // Assert
        result.Should().BeTrue();
        var activeAlerts = await _sut.GetActiveAlertsAsync();
        activeAlerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DismissAlertAsync_NonExistentAlert_ReturnsFalse()
    {
        var result = await _sut.DismissAlertAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_UnresolvedRecipe_ExcludedFromAlerts()
    {
        // Arrange
        var tomatoes = SeedCanonicalIngredient("Tomatoes");
        SeedInventoryItem(tomatoes.Id, 400m, GramId, Today().AddDays(2));
        SeedUnresolvedRecipe("Salsa", tomatoes.Id, 200m, GramId);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_CalledTwice_NoDuplicateAlerts()
    {
        // Arrange
        var yogurt = SeedCanonicalIngredient("Greek Yogurt");
        SeedInventoryItem(yogurt.Id, 500m, GramId, Today().AddDays(2));
        SeedFullyResolvedRecipe("Yogurt Parfait", "American",
            [(yogurt.Id, 200m, GramId)]);

        // Act
        await _sut.EvaluateAlertsAsync();
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().HaveCount(1);
        _dbContext.WasteAlerts.Count().Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_AlreadyExpiredItem_StillSurfacesAlert()
    {
        // Arrange — item expired yesterday, but still in inventory
        var milk = SeedCanonicalIngredient("Milk");
        SeedInventoryItem(milk.Id, 500m, MlId, Today().AddDays(-1));
        var recipe = SeedFullyResolvedRecipe("Pancakes", "American",
            [(milk.Id, 200m, MlId)]);

        // Act
        var alerts = await _sut.EvaluateAlertsAsync();

        // Assert
        alerts.Should().ContainSingle(a => a.CanonicalIngredientName == "Milk");
        alerts[0].DaysUntilExpiry.Should().BeNegative();
    }
}
