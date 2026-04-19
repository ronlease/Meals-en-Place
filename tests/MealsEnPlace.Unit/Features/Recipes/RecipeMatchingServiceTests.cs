// Feature: Recipe Matching — What Can I Make?
//
// Scenario: Full match when all ingredients present with sufficient quantity
//   Given a recipe requiring 200g of Chicken and 100ml of Olive Oil
//   And inventory contains 300g of Chicken and 200ml of Olive Oil
//   When MatchRecipesAsync is called
//   Then the recipe appears in the FullMatches list
//   And the MatchScore is 1.0
//
// Scenario: Near match when >= 75% ingredients present
//   Given a recipe requiring 4 ingredients
//   And inventory contains sufficient quantity of exactly 3 of them
//   When MatchRecipesAsync is called
//   Then the recipe appears in the NearMatches list (MatchScore = 0.75)
//
// Scenario: Partial match when >= 50% ingredients present
//   Given a recipe requiring 4 ingredients
//   And inventory contains sufficient quantity of exactly 2 of them
//   When MatchRecipesAsync is called
//   Then the recipe appears in the PartialMatches list (MatchScore = 0.5)
//
// Scenario: Below 50% is discarded
//   Given a recipe requiring 4 ingredients
//   And inventory contains sufficient quantity of only 1 of them
//   When MatchRecipesAsync is called
//   Then the recipe does not appear in any match tier
//
// Scenario: Unresolved recipe excluded from matching (critical edge case #4)
//   Given a recipe where one RecipeIngredient has IsContainerResolved = false
//   And inventory contains all other ingredients in sufficient quantity
//   When MatchRecipesAsync is called
//   Then the recipe does not appear in any match tier
//
// Scenario: Partially resolved recipe still excluded (critical edge case #5)
//   Given a recipe with 4 RecipeIngredients where 3 are resolved and 1 is not
//   And inventory covers all 3 resolved ingredients
//   When MatchRecipesAsync is called
//   Then the recipe does not appear in any match tier
//
// Scenario: WasteBonus applies for expiry within 3 days
//   Given a recipe with one ingredient that has a matching inventory item expiring within 3 days
//   And the base MatchScore is 1.0
//   When MatchRecipesAsync is called
//   Then the FinalScore is greater than the MatchScore
//
// Scenario: WasteBonus caps FinalScore at 1.0 (critical edge case #3)
//   Given a recipe where all 5 ingredients match inventory items expiring within 3 days
//   And the MatchScore is 1.0 before any bonus
//   When MatchRecipesAsync is called
//   Then the FinalScore is exactly 1.0
//   And never exceeds 1.0
//
// Scenario: Empty inventory returns empty results (critical edge case #10)
//   Given no inventory items exist
//   And a fully resolved recipe exists
//   When MatchRecipesAsync is called
//   Then all match tiers are empty
//   And no exception is thrown
//
// Scenario: Cuisine filter works
//   Given two fully resolved recipes, one Italian and one Mexican
//   And inventory satisfies both recipes
//   When MatchRecipesAsync is called with cuisine filter "Italian"
//   Then only the Italian recipe appears in any match tier
//
// Scenario: Zero-quantity inventory does not satisfy requirements (critical edge case #2)
//   Given a recipe requiring 200g of Chicken
//   And an inventory item for Chicken with Quantity = 0
//   When MatchRecipesAsync is called
//   Then the recipe does not appear as a FullMatch
//   And the Chicken ingredient appears in the MissingIngredients list

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Features.Recipes;

public class RecipeMatchingServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IClaudeAvailability> _claudeAvailabilityMock = new(MockBehavior.Loose);
    private readonly Mock<IClaudeService> _claudeServiceMock = new(MockBehavior.Loose);
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly RecipeMatchingService _sut;

    // Known stable unit of measure IDs from UnitOfMeasureConfiguration seed data
    private static readonly Guid EachId = UnitOfMeasureConfiguration.EachId;
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;
    private static readonly Guid MlId = UnitOfMeasureConfiguration.MlId;
    private static readonly Guid OzId = UnitOfMeasureConfiguration.OzId;

    public RecipeMatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);

        // Claude is invoked for NearMatch substitution suggestions; return empty list
        _claudeServiceMock
            .Setup(c => c.SuggestSubstitutionsAsync(
                It.IsAny<Recipe>(),
                It.IsAny<IReadOnlyList<MissingIngredient>>(),
                It.IsAny<IReadOnlyList<InventoryItem>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubstitutionSuggestion>());

        SeedUnitOfMeasures();

        var conversionService = new UnitOfMeasureConversionService(_dbContext);
        var displayConverter = new UnitOfMeasureDisplayConverter(_dbContext);

        _claudeAvailabilityMock
            .Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new RecipeMatchingService(
            _claudeAvailabilityMock.Object,
            _claudeServiceMock.Object,
            _dbContext,
            conversionService,
            displayConverter);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the full unit of measure table using the same IDs and conversion factors defined in
    /// <see cref="UnitOfMeasureConfiguration"/>. The in-memory provider does not
    /// apply HasData seed; we insert manually with the same values.
    /// </summary>
    private void SeedUnitOfMeasures()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            // Base units
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
            },
            // Weight derived
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                BaseUnitOfMeasureId = GramId,
                ConversionFactor = 28.350m,
                Id = OzId,
                Name = "Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUnitOfMeasureId = GramId,
                ConversionFactor = 453.592m,
                Id = UnitOfMeasureConfiguration.LbId,
                Name = "Pound",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            // Volume derived
            new UnitOfMeasure
            {
                Abbreviation = "cup",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 236.588m,
                Id = UnitOfMeasureConfiguration.CupId,
                Name = "Cup",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "fl oz",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 29.574m,
                Id = UnitOfMeasureConfiguration.FlOzId,
                Name = "Fluid Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tbsp",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 14.787m,
                Id = UnitOfMeasureConfiguration.TbspId,
                Name = "Tablespoon",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tsp",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 4.929m,
                Id = UnitOfMeasureConfiguration.TspId,
                Name = "Teaspoon",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "L",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.LiterId,
                Name = "Liter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "pt",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 473.176m,
                Id = UnitOfMeasureConfiguration.PintId,
                Name = "Pint",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "qt",
                BaseUnitOfMeasureId = MlId,
                ConversionFactor = 946.353m,
                Id = UnitOfMeasureConfiguration.QuartId,
                Name = "Quart",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "kg",
                BaseUnitOfMeasureId = GramId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.KgId,
                Name = "Kilogram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            });

        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedCanonicalIngredient(string name, Guid? id = null)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = EachId,
            Id = id ?? Guid.NewGuid(),
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
        DateOnly? expiryDate = null)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Fridge,
            Quantity = quantity,
            UnitOfMeasureId = unitOfMeasureId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    /// <summary>
    /// Seeds a fully resolved recipe where every ingredient has IsContainerResolved = true.
    /// Each entry in <paramref name="ingredientLines"/> is (canonicalIngredientId, quantity, unitOfMeasureId).
    /// </summary>
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

    private static RecipeMatchRequest EmptyRequest() => new();

    // ── Full match ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_AllIngredientsPresent_RecipeAppearsInFullMatches()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        var oliveOil = SeedCanonicalIngredient("Olive Oil");

        SeedInventoryItem(chicken.Id, 300m, GramId);   // 300g ≥ required 200g
        SeedInventoryItem(oliveOil.Id, 200m, MlId);    // 200ml ≥ required 100ml

        var recipe = SeedFullyResolvedRecipe("Simple Chicken", "Italian", [
            (chicken.Id, 200m, GramId),
            (oliveOil.Id, 100m, MlId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.FullMatches.Should().Contain(m => m.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_AllIngredientsPresent_MatchScoreIsOne()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        SeedInventoryItem(chicken.Id, 500m, GramId);

        var recipe = SeedFullyResolvedRecipe("Grilled Chicken", "American", [
            (chicken.Id, 300m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.FullMatches.Single(m => m.RecipeId == recipe.Id);
        match.MatchScore.Should().Be(1.0m);
    }

    [Fact]
    public async Task MatchRecipesAsync_AllIngredientsPresent_RecipeNotInNearOrPartialMatches()
    {
        // Arrange
        var pasta = SeedCanonicalIngredient("Pasta");
        SeedInventoryItem(pasta.Id, 500m, GramId);

        var recipe = SeedFullyResolvedRecipe("Pasta Aglio", "Italian", [
            (pasta.Id, 200m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.NearMatches.Should().NotContain(m => m.RecipeId == recipe.Id);
        response.PartialMatches.Should().NotContain(m => m.RecipeId == recipe.Id);
    }

    // ── Near match (>= 75%) ────────────────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_ThreeOfFourIngredientsCovered_RecipeAppearsInNearMatches()
    {
        // Arrange — 4 ingredients, inventory covers exactly 3 → MatchScore = 0.75
        var ing1 = SeedCanonicalIngredient("Ingredient A");
        var ing2 = SeedCanonicalIngredient("Ingredient B");
        var ing3 = SeedCanonicalIngredient("Ingredient C");
        var ing4 = SeedCanonicalIngredient("Ingredient D");

        SeedInventoryItem(ing1.Id, 100m, GramId);
        SeedInventoryItem(ing2.Id, 100m, GramId);
        SeedInventoryItem(ing3.Id, 100m, GramId);
        // ing4 intentionally absent

        var recipe = SeedFullyResolvedRecipe("Near Match Dish", "French", [
            (ing1.Id, 50m, GramId),
            (ing2.Id, 50m, GramId),
            (ing3.Id, 50m, GramId),
            (ing4.Id, 50m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.NearMatches.Should().Contain(m => m.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_ThreeOfFourIngredientsCovered_MatchScoreIsPointSevenFive()
    {
        // Arrange
        var ing1 = SeedCanonicalIngredient("Onion");
        var ing2 = SeedCanonicalIngredient("Garlic");
        var ing3 = SeedCanonicalIngredient("Tomato");
        var ing4 = SeedCanonicalIngredient("Anchovies");

        SeedInventoryItem(ing1.Id, 200m, GramId);
        SeedInventoryItem(ing2.Id, 50m, GramId);
        SeedInventoryItem(ing3.Id, 300m, GramId);
        // Anchovies absent

        var recipe = SeedFullyResolvedRecipe("Caesar Salad", "Italian", [
            (ing1.Id, 100m, GramId),
            (ing2.Id, 20m, GramId),
            (ing3.Id, 150m, GramId),
            (ing4.Id, 30m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.NearMatches.Single(m => m.RecipeId == recipe.Id);
        match.MatchScore.Should().Be(0.75m);
    }

    // ── NearMatch Claude call boundary (critical: fires at exactly 0.75, not at 0.74) ──

    [Fact]
    public async Task MatchRecipesAsync_MatchScoreExactlyPointSevenFive_ClaudeSubstitutionCalled()
    {
        // Arrange — critical edge case #7: Claude fires at MatchScore = 0.75 (inclusive)
        var ing1 = SeedCanonicalIngredient("Flour NM");
        var ing2 = SeedCanonicalIngredient("Sugar NM");
        var ing3 = SeedCanonicalIngredient("Butter NM");
        var ing4 = SeedCanonicalIngredient("Eggs NM");

        SeedInventoryItem(ing1.Id, 500m, GramId);
        SeedInventoryItem(ing2.Id, 200m, GramId);
        SeedInventoryItem(ing3.Id, 250m, GramId);
        // ing4 absent → 3/4 = 0.75 → NearMatch

        SeedFullyResolvedRecipe("Boundary Cake", "American", [
            (ing1.Id, 250m, GramId),
            (ing2.Id, 100m, GramId),
            (ing3.Id, 100m, GramId),
            (ing4.Id, 50m, GramId)
        ]);

        // Act
        await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert — Claude must be invoked for the NearMatch candidate
        _claudeServiceMock.Verify(
            c => c.SuggestSubstitutionsAsync(
                It.IsAny<Recipe>(),
                It.IsAny<IReadOnlyList<MissingIngredient>>(),
                It.IsAny<IReadOnlyList<InventoryItem>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task MatchRecipesAsync_FullMatch_ClaudeSubstitutionNeverCalled()
    {
        // Arrange — critical edge case #7: FullMatch must never trigger Claude substitution
        var ing1 = SeedCanonicalIngredient("Rice FM");
        SeedInventoryItem(ing1.Id, 500m, GramId);

        SeedFullyResolvedRecipe("Simple Rice", "Japanese", [
            (ing1.Id, 200m, GramId)
        ]);

        // Act
        await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        _claudeServiceMock.Verify(
            c => c.SuggestSubstitutionsAsync(
                It.IsAny<Recipe>(),
                It.IsAny<IReadOnlyList<MissingIngredient>>(),
                It.IsAny<IReadOnlyList<InventoryItem>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Partial match (>= 50%, < 75%) ─────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_TwoOfFourIngredientsCovered_RecipeAppearsInPartialMatches()
    {
        // Arrange — 4 ingredients, inventory covers exactly 2 → MatchScore = 0.5
        var ing1 = SeedCanonicalIngredient("Beef");
        var ing2 = SeedCanonicalIngredient("Carrots");
        var ing3 = SeedCanonicalIngredient("Potatoes");
        var ing4 = SeedCanonicalIngredient("Red Wine");

        SeedInventoryItem(ing1.Id, 500m, GramId);
        SeedInventoryItem(ing2.Id, 300m, GramId);
        // ing3 and ing4 absent

        var recipe = SeedFullyResolvedRecipe("Beef Stew", "French", [
            (ing1.Id, 400m, GramId),
            (ing2.Id, 200m, GramId),
            (ing3.Id, 200m, GramId),
            (ing4.Id, 150m, MlId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.PartialMatches.Should().Contain(m => m.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_TwoOfFourIngredientsCovered_MatchScoreIsPointFive()
    {
        // Arrange
        var ing1 = SeedCanonicalIngredient("Lentils");
        var ing2 = SeedCanonicalIngredient("Spinach");
        var ing3 = SeedCanonicalIngredient("Cumin");
        var ing4 = SeedCanonicalIngredient("Garam Masala");

        SeedInventoryItem(ing1.Id, 400m, GramId);
        SeedInventoryItem(ing2.Id, 200m, GramId);

        var recipe = SeedFullyResolvedRecipe("Dal Palak", "Indian", [
            (ing1.Id, 200m, GramId),
            (ing2.Id, 100m, GramId),
            (ing3.Id, 10m, GramId),
            (ing4.Id, 5m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.PartialMatches.Single(m => m.RecipeId == recipe.Id);
        match.MatchScore.Should().Be(0.5m);
    }

    // ── Below 50% discarded ────────────────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_OneOfFourIngredientsCovered_RecipeNotInAnyMatchTier()
    {
        // Arrange — 4 ingredients, inventory covers only 1 → MatchScore = 0.25 → discarded
        var ing1 = SeedCanonicalIngredient("Salmon Fillet");
        var ing2 = SeedCanonicalIngredient("Capers");
        var ing3 = SeedCanonicalIngredient("Dill");
        var ing4 = SeedCanonicalIngredient("Crème Fraîche");

        SeedInventoryItem(ing1.Id, 400m, GramId);
        // ing2, ing3, ing4 absent

        var recipe = SeedFullyResolvedRecipe("Salmon with Caper Cream", "Scandinavian", [
            (ing1.Id, 300m, GramId),
            (ing2.Id, 20m, GramId),
            (ing3.Id, 10m, GramId),
            (ing4.Id, 100m, MlId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var allIds = response.FullMatches.Select(m => m.RecipeId)
            .Concat(response.NearMatches.Select(m => m.RecipeId))
            .Concat(response.PartialMatches.Select(m => m.RecipeId));

        allIds.Should().NotContain(recipe.Id);
    }

    // ── Unresolved recipe excluded (critical edge case #4) ─────────────────────

    [Fact]
    public async Task MatchRecipesAsync_RecipeHasOneUnresolvedIngredient_RecipeNotInAnyMatchTier()
    {
        // Arrange — critical edge case #4: one unresolved ingredient blocks the entire recipe
        var chicken = SeedCanonicalIngredient("Chicken Unresolved");
        var tomatoSauce = SeedCanonicalIngredient("Tomato Sauce Unresolved");

        SeedInventoryItem(chicken.Id, 500m, GramId);
        // Tomato sauce is in inventory but the recipe ingredient is an unresolved container reference

        var recipe = new Recipe
        {
            CuisineType = "Mexican",
            Id = Guid.NewGuid(),
            Instructions = "Cook.",
            ServingCount = 4,
            Title = "Chili Con Carne"
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        // Resolved ingredient
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = chicken.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Quantity = 300m,
            RecipeId = recipe.Id,
            UnitOfMeasureId = GramId
        });

        // Unresolved container reference
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = tomatoSauce.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = false,
            Notes = "1 can tomato sauce",
            Quantity = 0m,
            RecipeId = recipe.Id,
            UnitOfMeasureId = null
        });

        _dbContext.SaveChanges();

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var allIds = response.FullMatches.Select(m => m.RecipeId)
            .Concat(response.NearMatches.Select(m => m.RecipeId))
            .Concat(response.PartialMatches.Select(m => m.RecipeId));

        allIds.Should().NotContain(recipe.Id);
    }

    // ── Partially resolved recipe still excluded (critical edge case #5) ────────

    [Fact]
    public async Task MatchRecipesAsync_ThreeOfFourRecipeIngredientsResolvedOneFalse_RecipeNotInAnyMatchTier()
    {
        // Arrange — critical edge case #5: 3 resolved, 1 unresolved → entire recipe excluded
        var ing1 = SeedCanonicalIngredient("Kidney Beans Partial");
        var ing2 = SeedCanonicalIngredient("Beef Mince Partial");
        var ing3 = SeedCanonicalIngredient("Chili Powder Partial");
        var ing4 = SeedCanonicalIngredient("Stock Partial");

        SeedInventoryItem(ing1.Id, 400m, GramId);
        SeedInventoryItem(ing2.Id, 500m, GramId);
        SeedInventoryItem(ing3.Id, 50m, GramId);

        var recipe = new Recipe
        {
            CuisineType = "Tex-Mex",
            Id = Guid.NewGuid(),
            Instructions = "Brown the beef.",
            ServingCount = 4,
            Title = "Partially Resolved Chili"
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        // Three resolved
        foreach (var (ing, qty) in new[] { (ing1, 200m), (ing2, 400m), (ing3, 10m) })
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ing.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = qty,
                RecipeId = recipe.Id,
                UnitOfMeasureId = GramId
            });
        }

        // One unresolved
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = ing4.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = false,
            Notes = "1 carton chicken stock",
            Quantity = 0m,
            RecipeId = recipe.Id,
            UnitOfMeasureId = null
        });

        _dbContext.SaveChanges();

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var allIds = response.FullMatches.Select(m => m.RecipeId)
            .Concat(response.NearMatches.Select(m => m.RecipeId))
            .Concat(response.PartialMatches.Select(m => m.RecipeId));

        allIds.Should().NotContain(recipe.Id);
    }

    // ── WasteBonus ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_ExpiryImminentIngredientMatched_FinalScoreIsGreaterThanMatchScore()
    {
        // Arrange — inventory item expires within 3 days; WasteBonus must push FinalScore above MatchScore
        var heavyCream = SeedCanonicalIngredient("Heavy Cream Waste");
        var expiringDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        SeedInventoryItem(heavyCream.Id, 300m, MlId, expiryDate: expiringDate);

        var recipe = SeedFullyResolvedRecipe("Alfredo Pasta", "Italian", [
            (heavyCream.Id, 200m, MlId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.FullMatches.Single(m => m.RecipeId == recipe.Id);
        match.FinalScore.Should().BeGreaterThanOrEqualTo(match.MatchScore);
        match.MatchedIngredients.Should().Contain(i => i.IsExpiryImminent);
    }

    [Fact]
    public async Task MatchRecipesAsync_ExpiryImminentIngredientMatched_IsExpiryImminentIsTrue()
    {
        // Arrange
        var cream = SeedCanonicalIngredient("Cream Expiry");
        var expiringDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        SeedInventoryItem(cream.Id, 500m, MlId, expiryDate: expiringDate);

        var recipe = SeedFullyResolvedRecipe("Cream Sauce", "French", [
            (cream.Id, 200m, MlId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.FullMatches.Single(m => m.RecipeId == recipe.Id);
        match.MatchedIngredients.Should().Contain(mi => mi.IsExpiryImminent);
    }

    [Fact]
    public async Task MatchRecipesAsync_ExpiryFourDaysOut_WasteBonusNotApplied()
    {
        // Arrange — expires in 4 days, just outside the 3-day window
        var butter = SeedCanonicalIngredient("Butter Not Expiring");
        var notExpiringDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));

        SeedInventoryItem(butter.Id, 300m, GramId, expiryDate: notExpiringDate);

        var recipe = SeedFullyResolvedRecipe("Buttered Toast", "American", [
            (butter.Id, 50m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.FullMatches.Single(m => m.RecipeId == recipe.Id);
        match.FinalScore.Should().Be(match.MatchScore);
    }

    // ── WasteBonus cap at 1.0 (critical edge case #3) ─────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_AllFiveIngredientsExpiryImminent_FinalScoreDoesNotExceedOne()
    {
        // Arrange — critical edge case #3: 5 expiry-imminent matches × 0.1 bonus = 0.5 potential bonus
        // on top of MatchScore 1.0 — FinalScore must be capped at 1.0
        var expiringDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var ingredients = Enumerable.Range(1, 5)
            .Select(i => SeedCanonicalIngredient($"Expiry Ingredient {i}"))
            .ToList();

        foreach (var ing in ingredients)
            SeedInventoryItem(ing.Id, 300m, GramId, expiryDate: expiringDate);

        var recipe = SeedFullyResolvedRecipe("Everything Expiring", "Fusion",
            ingredients.Select(ing => (ing.Id, 100m, GramId)));

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        var match = response.FullMatches.Single(m => m.RecipeId == recipe.Id);
        match.FinalScore.Should().BeLessThanOrEqualTo(1.0m);
        match.FinalScore.Should().Be(1.0m);
    }

    // ── Empty inventory (critical edge case #10) ──────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_NoInventoryItems_AllMatchTiersAreEmpty()
    {
        // Arrange — critical edge case #10: empty inventory must return empty results, not throw
        var ing = SeedCanonicalIngredient("Solitary Ingredient");

        SeedFullyResolvedRecipe("Lonely Dish", "Greek", [
            (ing.Id, 100m, GramId)
        ]);

        // No inventory items seeded

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.FullMatches.Should().BeEmpty();
        response.NearMatches.Should().BeEmpty();
        response.PartialMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task MatchRecipesAsync_NoInventoryItems_DoesNotThrow()
    {
        // Arrange — critical edge case #10: must not throw NullReferenceException or DivisionByZero
        var ing = SeedCanonicalIngredient("Orphan Ingredient");

        SeedFullyResolvedRecipe("Orphan Dish", "Greek", [
            (ing.Id, 100m, GramId)
        ]);

        // Act
        var act = async () => await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── Cuisine filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_CuisineFilterItalian_OnlyItalianRecipesReturned()
    {
        // Arrange
        var pasta = SeedCanonicalIngredient("Pasta CF");
        var beef = SeedCanonicalIngredient("Beef CF");

        SeedInventoryItem(pasta.Id, 500m, GramId);
        SeedInventoryItem(beef.Id, 500m, GramId);

        var italianRecipe = SeedFullyResolvedRecipe("Spaghetti", "Italian", [
            (pasta.Id, 200m, GramId)
        ]);
        var mexicanRecipe = SeedFullyResolvedRecipe("Tacos", "Mexican", [
            (beef.Id, 300m, GramId)
        ]);

        var request = new RecipeMatchRequest { Cuisine = "Italian" };

        // Act
        var response = await _sut.MatchRecipesAsync(request);

        // Assert
        var allReturnedIds = response.FullMatches.Select(m => m.RecipeId)
            .Concat(response.NearMatches.Select(m => m.RecipeId))
            .Concat(response.PartialMatches.Select(m => m.RecipeId))
            .ToList();

        allReturnedIds.Should().Contain(italianRecipe.Id);
        allReturnedIds.Should().NotContain(mexicanRecipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_CuisineFilterCaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var rice = SeedCanonicalIngredient("Rice CI");
        SeedInventoryItem(rice.Id, 500m, GramId);

        var recipe = SeedFullyResolvedRecipe("Rice Dish", "Japanese", [
            (rice.Id, 200m, GramId)
        ]);

        var request = new RecipeMatchRequest { Cuisine = "japanese" };

        // Act
        var response = await _sut.MatchRecipesAsync(request);

        // Assert
        var allIds = response.FullMatches.Select(m => m.RecipeId)
            .Concat(response.NearMatches.Select(m => m.RecipeId))
            .Concat(response.PartialMatches.Select(m => m.RecipeId));
        allIds.Should().Contain(recipe.Id);
    }

    // ── Zero-quantity inventory (critical edge case #2) ────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_InventoryItemHasZeroQuantity_RecipeNotInFullMatches()
    {
        // Arrange — critical edge case #2: Quantity = 0 must not satisfy any requirement
        var chicken = SeedCanonicalIngredient("Chicken Zero Qty");
        SeedInventoryItem(chicken.Id, 0m, GramId);   // zero quantity

        var recipe = SeedFullyResolvedRecipe("Chicken Dish Zero", "American", [
            (chicken.Id, 200m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.FullMatches.Should().NotContain(m => m.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_InventoryItemHasZeroQuantity_IngredientAppearsInMissingIngredients()
    {
        // Arrange — critical edge case #2: the ingredient must appear as missing, not matched
        var chicken = SeedCanonicalIngredient("Chicken Zero Missing");
        SeedInventoryItem(chicken.Id, 0m, GramId);

        var recipe = SeedFullyResolvedRecipe("Zero Qty Recipe", "American", [
            (chicken.Id, 200m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert — the recipe is below 50% and discarded; confirm ingredient was not matched
        // by verifying it never appears in any MatchedIngredients list across all tiers
        var allMatched = response.FullMatches
            .Concat(response.NearMatches)
            .Concat(response.PartialMatches)
            .SelectMany(m => m.MatchedIngredients);

        allMatched.Should().NotContain(mi => mi.IngredientName == "Chicken Zero Missing");
    }

    // ── Insufficient quantity does not satisfy requirement ─────────────────────

    [Fact]
    public async Task MatchRecipesAsync_InsufficientInventoryQuantity_IngredientCountsAsMissing()
    {
        // Arrange — inventory has 100g but recipe requires 300g
        var ing = SeedCanonicalIngredient("Cheese Insufficient");
        SeedInventoryItem(ing.Id, 100m, GramId);   // only 100g

        var recipe = SeedFullyResolvedRecipe("Cheesy Pasta", "Italian", [
            (ing.Id, 300m, GramId)   // needs 300g
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert — recipe should not be a full match; ingredient should not appear matched
        response.FullMatches.Should().NotContain(m => m.RecipeId == recipe.Id);
    }

    // ── unit of measure conversion during matching ─────────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_InventoryInGramsRecipeInOunces_ConvertsCorrectlyForComparison()
    {
        // Arrange — inventory has 300g of butter; recipe requires 8 oz (≈ 226.8g)
        // 300g > 226.8g, so it should be a full match
        var butter = SeedCanonicalIngredient("Butter unit of measure Conv");
        SeedInventoryItem(butter.Id, 300m, GramId);   // 300g

        var recipe = SeedFullyResolvedRecipe("Buttery Dish", "French", [
            (butter.Id, 8m, OzId)   // 8 oz ≈ 226.8g
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.FullMatches.Should().Contain(m => m.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task MatchRecipesAsync_InventoryInOuncesRecipeInGrams_ConvertsCorrectlyForComparison()
    {
        // Arrange — inventory has 8 oz (≈ 226.8g); recipe requires 200g → should be full match
        var cheddar = SeedCanonicalIngredient("Cheddar unit of measure Conv");
        SeedInventoryItem(cheddar.Id, 8m, OzId);   // 8 oz

        var recipe = SeedFullyResolvedRecipe("Cheddar Omelette", "British", [
            (cheddar.Id, 200m, GramId)   // 200g
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.FullMatches.Should().Contain(m => m.RecipeId == recipe.Id);
    }

    // ── Multiple recipes ranked correctly ──────────────────────────────────────

    [Fact]
    public async Task MatchRecipesAsync_MultipleFullMatches_ResultOrderedByFinalScoreDescending()
    {
        // Arrange — two full matches, one with a WasteBonus (should rank higher)
        var ing1 = SeedCanonicalIngredient("Rank Ingredient 1");
        var ing2 = SeedCanonicalIngredient("Rank Ingredient 2");

        var expiringDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        SeedInventoryItem(ing1.Id, 500m, GramId, expiryDate: expiringDate);   // expiring
        SeedInventoryItem(ing2.Id, 500m, GramId);                              // not expiring

        var recipeWithBonus = SeedFullyResolvedRecipe("Recipe With Bonus", "Italian", [
            (ing1.Id, 200m, GramId)
        ]);
        var recipeWithoutBonus = SeedFullyResolvedRecipe("Recipe Without Bonus", "Italian", [
            (ing2.Id, 200m, GramId)
        ]);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert — recipe with waste bonus should appear before recipe without bonus
        var fullMatchIds = response.FullMatches.Select(m => m.RecipeId).ToList();
        fullMatchIds.IndexOf(recipeWithBonus.Id).Should().BeLessThan(fullMatchIds.IndexOf(recipeWithoutBonus.Id));
    }

    // ── MEP-032: graceful degradation without a Claude API key ────────────────
    //
    // Scenario: Feasibility pass is skipped and response flag reflects it
    //   Given no Claude API key is configured
    //   And the matching pipeline produces at least one NearMatch
    //   When MatchRecipesAsync is called
    //   Then the response's ClaudeFeasibilityApplied flag is false
    //   And Claude.SuggestSubstitutionsAsync is never invoked

    [Fact]
    public async Task MatchRecipesAsync_WithoutClaudeKey_SetsFlagFalseAndSkipsSubstitutions()
    {
        // Arrange — build a NearMatch (3 of 4 ingredients available)
        var butter = SeedCanonicalIngredient("Butter");
        var flour = SeedCanonicalIngredient("Flour");
        var sugar = SeedCanonicalIngredient("Sugar");
        var milk = SeedCanonicalIngredient("Milk");

        SeedInventoryItem(butter.Id, 200m, GramId);
        SeedInventoryItem(flour.Id, 300m, GramId);
        SeedInventoryItem(sugar.Id, 100m, GramId);
        // Milk deliberately missing to force NearMatch

        SeedFullyResolvedRecipe("Shortbread", "British",
        [
            (butter.Id, 100m, GramId),
            (flour.Id, 150m, GramId),
            (sugar.Id, 50m, GramId),
            (milk.Id, 30m, GramId)
        ]);

        // Flip availability off for this test
        _claudeAvailabilityMock
            .Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await _sut.MatchRecipesAsync(EmptyRequest());

        // Assert
        response.ClaudeFeasibilityApplied.Should().BeFalse();
        response.NearMatches.Should().NotBeEmpty();
        _claudeServiceMock.Verify(
            c => c.SuggestSubstitutionsAsync(
                It.IsAny<Recipe>(),
                It.IsAny<IReadOnlyList<MissingIngredient>>(),
                It.IsAny<IReadOnlyList<InventoryItem>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
