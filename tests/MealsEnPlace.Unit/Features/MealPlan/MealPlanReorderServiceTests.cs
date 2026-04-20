// Feature: Reorder Meal Plan to Prioritize Expiring Ingredients (MEP-030)
//
// Scenario: No planned recipe touches an expiring ingredient → HasChanges=false with explanatory reason
// Scenario: A recipe using a near-expiry ingredient moves earlier within its MealSlot
// Scenario: MealSlot boundaries are respected (Breakfast stays Breakfast, Lunch stays Lunch)
// Scenario: Equal-urgency recipes retain their relative order (stable sort)
// Scenario: Applying a reorder persists the new DayOfWeek assignments
// Scenario: Already-expired ingredients score as maximum urgency

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.MealPlan;

public sealed class MealPlanReorderServiceTests : IDisposable
{
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly MealPlanReorderService _sut;

    public MealPlanReorderServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        SeedGramUnit();

        // Reorder service only ever calls GetMealPlanAsync on the plan service
        // after Apply. A narrow stub keeps the test scope tight.
        var planServiceStub = new StubMealPlanService(_dbContext);
        _sut = new MealPlanReorderService(_dbContext, planServiceStub);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task PreviewAsync_NoIngredientInWindow_ReturnsHasChangesFalseWithReason()
    {
        // Arrange — chicken with distant expiry, pasta with no expiry.
        var chicken = SeedIngredient("Chicken");
        var pasta = SeedIngredient("Pasta");
        SeedInventoryItem(chicken.Id, 500m, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60));
        SeedInventoryItem(pasta.Id, 500m, expiryDate: null);

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Dinner, "Grilled Chicken", (chicken.Id, 200m)),
            (DayOfWeek.Tuesday, MealSlot.Dinner, "Pasta Aglio", (pasta.Id, 150m)));

        // Act
        var preview = await _sut.PreviewAsync(plan.Id, urgencyWindowDays: 7);

        // Assert
        preview.Should().NotBeNull();
        preview!.HasChanges.Should().BeFalse();
        preview.Reason.Should().NotBeNullOrWhiteSpace();
        preview.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_ExpiringIngredient_MovesRecipeEarlier()
    {
        // Arrange — salmon expires in 2 days, pasta doesn't expire. Recipe
        // order is pasta first, salmon second; reorder should swap them.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var salmon = SeedIngredient("Salmon");
        var pasta = SeedIngredient("Pasta");
        SeedInventoryItem(salmon.Id, 200m, today.AddDays(2));
        SeedInventoryItem(pasta.Id, 500m, expiryDate: null);

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Dinner, "Pasta Aglio", (pasta.Id, 150m)),
            (DayOfWeek.Tuesday, MealSlot.Dinner, "Grilled Salmon", (salmon.Id, 180m)));

        // Act
        var preview = await _sut.PreviewAsync(plan.Id, urgencyWindowDays: 7);

        // Assert — salmon moves to Monday, pasta to Tuesday
        preview!.HasChanges.Should().BeTrue();
        preview.Changes.Should().HaveCount(2);
        var salmonChange = preview.Changes.Single(c => c.RecipeTitle == "Grilled Salmon");
        salmonChange.OriginalDay.Should().Be(DayOfWeek.Tuesday);
        salmonChange.ProposedDay.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task PreviewAsync_PreservesMealSlotBoundaries()
    {
        // Arrange — urgency only helps Breakfast; Lunch recipes must stay put.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var eggs = SeedIngredient("Eggs");
        var lettuce = SeedIngredient("Lettuce");
        SeedInventoryItem(eggs.Id, 12m, today.AddDays(1));
        SeedInventoryItem(lettuce.Id, 200m, expiryDate: null);

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Lunch, "Salad", (lettuce.Id, 100m)),
            (DayOfWeek.Wednesday, MealSlot.Breakfast, "Omelet", (eggs.Id, 3m)),
            (DayOfWeek.Tuesday, MealSlot.Breakfast, "Toast", (lettuce.Id, 10m)));

        // Act
        var preview = await _sut.PreviewAsync(plan.Id, urgencyWindowDays: 7);

        // Assert — the Breakfast pair swaps (Omelet to Tuesday / earliest
        // Breakfast day). The Lunch slot should never appear in changes.
        preview!.HasChanges.Should().BeTrue();
        preview.Changes.Should().OnlyContain(c => c.MealSlot == MealSlot.Breakfast);
    }

    [Fact]
    public async Task PreviewAsync_EqualUrgency_PreservesRelativeOrder()
    {
        // Arrange — two recipes with exactly the same urgency (both use an
        // ingredient expiring in 3 days). Order between them should not flip.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var beef = SeedIngredient("Beef");
        var chicken = SeedIngredient("Chicken");
        SeedInventoryItem(beef.Id, 300m, today.AddDays(3));
        SeedInventoryItem(chicken.Id, 300m, today.AddDays(3));

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Dinner, "Beef Stew", (beef.Id, 200m)),
            (DayOfWeek.Tuesday, MealSlot.Dinner, "Roast Chicken", (chicken.Id, 200m)));

        // Act
        var preview = await _sut.PreviewAsync(plan.Id, urgencyWindowDays: 7);

        // Assert — both have equal urgency; nothing moves.
        preview!.HasChanges.Should().BeFalse();
        preview.Reason.Should().Contain("already prioritizes");
    }

    [Fact]
    public async Task ApplyAsync_PersistsNewDayAssignments()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tofu = SeedIngredient("Tofu");
        var rice = SeedIngredient("Rice");
        SeedInventoryItem(tofu.Id, 200m, today.AddDays(1));
        SeedInventoryItem(rice.Id, 1000m, expiryDate: null);

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Dinner, "Rice Bowl", (rice.Id, 150m)),
            (DayOfWeek.Wednesday, MealSlot.Dinner, "Tofu Stir-fry", (tofu.Id, 150m)));

        // Act
        var response = await _sut.ApplyAsync(plan.Id, urgencyWindowDays: 7);

        // Assert — persistence on the Tofu slot (should now be Monday).
        response.Should().NotBeNull();
        var tofuSlot = await _dbContext.MealPlanSlots
            .AsNoTracking()
            .FirstAsync(s => s.Recipe.Title == "Tofu Stir-fry");
        tofuSlot.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task PreviewAsync_ExpiredIngredient_ScoresMaximumUrgency()
    {
        // Arrange — tomato expired yesterday, chicken fresh. Tomato must rank
        // first even though "days until expiry" is negative.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomato = SeedIngredient("Tomato");
        var chicken = SeedIngredient("Chicken");
        SeedInventoryItem(tomato.Id, 200m, today.AddDays(-1));
        SeedInventoryItem(chicken.Id, 500m, today.AddDays(5));

        var plan = SeedPlanWithSlots(
            (DayOfWeek.Monday, MealSlot.Dinner, "Grilled Chicken", (chicken.Id, 200m)),
            (DayOfWeek.Friday, MealSlot.Dinner, "Tomato Soup", (tomato.Id, 150m)));

        // Act
        var preview = await _sut.PreviewAsync(plan.Id, urgencyWindowDays: 7);

        // Assert — Tomato Soup should move to Monday.
        preview!.HasChanges.Should().BeTrue();
        var tomatoChange = preview.Changes.Single(c => c.RecipeTitle == "Tomato Soup");
        tomatoChange.ProposedDay.Should().Be(DayOfWeek.Monday);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedGramUnit()
    {
        _dbContext.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1m,
            Id = GramId,
            Name = "Gram",
            UnitOfMeasureType = UnitOfMeasureType.Weight
        });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedIngredient(string name)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = GramId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private void SeedInventoryItem(Guid canonicalIngredientId, decimal quantity, DateOnly? expiryDate)
    {
        _dbContext.InventoryItems.Add(new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Fridge,
            Quantity = quantity,
            UnitOfMeasureId = GramId
        });
        _dbContext.SaveChanges();
    }

    private MealsEnPlace.Api.Models.Entities.MealPlan SeedPlanWithSlots(
        params (DayOfWeek Day, MealSlot MealSlot, string RecipeTitle, (Guid IngredientId, decimal Quantity) IngredientLine)[] slotDefs)
    {
        var plan = new MealsEnPlace.Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);
        _dbContext.SaveChanges();

        foreach (var (day, mealSlot, recipeTitle, ingredientLine) in slotDefs)
        {
            var recipe = new Recipe
            {
                CuisineType = "Test",
                Id = Guid.NewGuid(),
                Instructions = "Cook.",
                ServingCount = 1,
                Title = recipeTitle
            };
            _dbContext.Recipes.Add(recipe);
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ingredientLine.IngredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = ingredientLine.Quantity,
                RecipeId = recipe.Id,
                UnitOfMeasureId = GramId
            });
            _dbContext.MealPlanSlots.Add(new MealPlanSlot
            {
                DayOfWeek = day,
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                MealSlot = mealSlot,
                RecipeId = recipe.Id
            });
        }
        _dbContext.SaveChanges();
        return plan;
    }

    /// <summary>
    /// Minimal <see cref="IMealPlanService"/> that only implements the single
    /// method the reorder service calls after Apply. The rest throw so any
    /// unintended call during the test fails loudly.
    /// </summary>
    private sealed class StubMealPlanService(MealsEnPlaceDbContext dbContext) : IMealPlanService
    {
        public Task<MealPlanResponse> GenerateMealPlanAsync(
            GenerateMealPlanRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MealPlanResponse?> GetActiveMealPlanAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public async Task<MealPlanResponse?> GetMealPlanAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var plan = await dbContext.MealPlans
                .AsNoTracking()
                .Include(mp => mp.Slots).ThenInclude(s => s.Recipe)
                .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);

            if (plan is null) return null;

            return new MealPlanResponse
            {
                CreatedAt = plan.CreatedAt,
                Id = plan.Id,
                Name = plan.Name,
                Slots = plan.Slots.Select(s => new MealPlanSlotResponse
                {
                    ConsumedAt = s.ConsumedAt,
                    CuisineType = s.Recipe.CuisineType,
                    DayOfWeek = s.DayOfWeek,
                    Id = s.Id,
                    MealSlot = s.MealSlot,
                    RecipeId = s.RecipeId,
                    RecipeTitle = s.Recipe.Title
                }).ToList(),
                WeekStartDate = plan.WeekStartDate
            };
        }

        public Task<List<MealPlanResponse>> ListMealPlansAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MealPlanSlotResponse?> SwapSlotAsync(
            Guid slotId, SwapSlotRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
