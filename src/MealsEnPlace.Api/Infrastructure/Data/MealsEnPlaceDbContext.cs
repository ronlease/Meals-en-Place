using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext for the Meals en Place application.
/// All entity configurations are applied via <see cref="IEntityTypeConfiguration{TEntity}"/>
/// classes discovered from this assembly.
/// </summary>
public class MealsEnPlaceDbContext(DbContextOptions<MealsEnPlaceDbContext> options)
    : DbContext(options)
{
    /// <summary>Canonical normalized ingredients.</summary>
    public DbSet<CanonicalIngredient> CanonicalIngredients => Set<CanonicalIngredient>();

    /// <summary>Historical name variants captured by the MEP-038 dedup pass.</summary>
    public DbSet<CanonicalIngredientAlias> CanonicalIngredientAliases => Set<CanonicalIngredientAlias>();

    /// <summary>Per-decrement audit rows for auto-deplete consume events (MEP-027 / MEP-031).</summary>
    public DbSet<ConsumeAuditEntry> ConsumeAuditEntries => Set<ConsumeAuditEntry>();

    /// <summary>Idempotency mapping from local entities to external task providers (MEP-028 / MEP-029).</summary>
    public DbSet<ExternalTaskLink> ExternalTaskLinks => Set<ExternalTaskLink>();

    /// <summary>Pantry, fridge, and freezer inventory items.</summary>
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    /// <summary>Weekly meal plans.</summary>
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();

    /// <summary>Individual day/slot assignments within a meal plan.</summary>
    public DbSet<MealPlanSlot> MealPlanSlots => Set<MealPlanSlot>();

    /// <summary>Local recipe library.</summary>
    public DbSet<Recipe> Recipes => Set<Recipe>();

    /// <summary>Dietary tag assignments for recipes.</summary>
    public DbSet<RecipeDietaryTag> RecipeDietaryTags => Set<RecipeDietaryTag>();

    /// <summary>Ingredient lines within recipes.</summary>
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    /// <summary>Seasonality windows for Zone 7a produce.</summary>
    public DbSet<SeasonalityWindow> SeasonalityWindows => Set<SeasonalityWindow>();

    /// <summary>Derived shopping list items from active meal plans.</summary>
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    /// <summary>Alias strings that map to canonical <see cref="UnitOfMeasure"/> rows.</summary>
    public DbSet<UnitOfMeasureAlias> UnitOfMeasureAliases => Set<UnitOfMeasureAlias>();

    /// <summary>Canonical units of measure with conversion factors.</summary>
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    /// <summary>Unit tokens deferred for user review during ingest.</summary>
    public DbSet<UnresolvedUnitOfMeasureToken> UnresolvedUnitOfMeasureTokens => Set<UnresolvedUnitOfMeasureToken>();

    /// <summary>Single-row application preferences table.</summary>
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    /// <summary>Expiry-imminent waste alerts.</summary>
    public DbSet<WasteAlert> WasteAlerts => Set<WasteAlert>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MealsEnPlaceDbContext).Assembly);
    }
}
