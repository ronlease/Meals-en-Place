using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Orchestrates the MEP-038 dedup pass: loads every
/// <see cref="CanonicalIngredient"/> into memory, normalizes names into
/// fold-group keys, selects a survivor per group, and reassigns every
/// foreign key from the loser rows to the survivor before inserting an
/// alias row and deleting each loser.
/// <para>
/// Writes happen in batches of <see cref="DedupConstants.FoldGroupBatchSize"/>
/// fold groups per database transaction so a failure in one group doesn't
/// leave the whole run half-applied. <c>--dry-run</c> mode runs the entire
/// plan through the resolver and populates the summary without touching the
/// database.
/// </para>
/// </summary>
internal sealed class CanonicalIngredientDedupRunner
{
    private readonly CanonicalNameNormalizer _normalizer;

    public CanonicalIngredientDedupRunner(CanonicalNameNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    /// <summary>
    /// Runs the dedup pass against <paramref name="dbContext"/>. The summary
    /// is populated in place so the CLI can format and print it when the
    /// method returns.
    /// </summary>
    public async Task RunAsync(
        MealsEnPlaceDbContext dbContext,
        DedupSummary summary,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(dbContext, cancellationToken);
        summary.CanonicalIngredientsLoaded = snapshot.Candidates.Count;

        var foldGroups = FoldGroupResolver.Resolve(snapshot.Candidates);
        summary.FoldGroupCount = foldGroups.Count;

        if (dryRun)
        {
            ProjectSummaryForDryRun(foldGroups, snapshot, summary);
            return;
        }

        for (var i = 0; i < foldGroups.Count; i += DedupConstants.FoldGroupBatchSize)
        {
            var batch = foldGroups.Skip(i).Take(DedupConstants.FoldGroupBatchSize).ToList();
            await ApplyBatchAsync(dbContext, batch, summary, cancellationToken);
        }
    }

    /// <summary>
    /// Loads every <see cref="CanonicalIngredient"/> along with per-table
    /// foreign-key usage counts. Done as five grouped queries to avoid N+1
    /// work against a 146k-row table. The dicts are kept so the dry-run
    /// projection can report an accurate per-table breakdown.
    /// </summary>
    private async Task<DedupSnapshot> LoadSnapshotAsync(
        MealsEnPlaceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var canonicalIngredients = await dbContext.CanonicalIngredients
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var recipeIngredientCounts = await dbContext.RecipeIngredients
            .AsNoTracking()
            .GroupBy(r => r.CanonicalIngredientId)
            .Select(g => new { CanonicalIngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CanonicalIngredientId, x => x.Count, cancellationToken);

        var inventoryItemCounts = await dbContext.InventoryItems
            .AsNoTracking()
            .GroupBy(i => i.CanonicalIngredientId)
            .Select(g => new { CanonicalIngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CanonicalIngredientId, x => x.Count, cancellationToken);

        var shoppingListItemCounts = await dbContext.ShoppingListItems
            .AsNoTracking()
            .GroupBy(s => s.CanonicalIngredientId)
            .Select(g => new { CanonicalIngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CanonicalIngredientId, x => x.Count, cancellationToken);

        var seasonalityWindowCounts = await dbContext.SeasonalityWindows
            .AsNoTracking()
            .GroupBy(s => s.CanonicalIngredientId)
            .Select(g => new { CanonicalIngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CanonicalIngredientId, x => x.Count, cancellationToken);

        var consumeAuditCounts = await dbContext.ConsumeAuditEntries
            .AsNoTracking()
            .GroupBy(a => a.CanonicalIngredientId)
            .Select(g => new { CanonicalIngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CanonicalIngredientId, x => x.Count, cancellationToken);

        var candidates = canonicalIngredients.Select(c =>
        {
            var count =
                recipeIngredientCounts.GetValueOrDefault(c.Id)
                + inventoryItemCounts.GetValueOrDefault(c.Id)
                + shoppingListItemCounts.GetValueOrDefault(c.Id)
                + seasonalityWindowCounts.GetValueOrDefault(c.Id)
                + consumeAuditCounts.GetValueOrDefault(c.Id);

            return new CanonicalIngredientFoldCandidate
            {
                Id = c.Id,
                Name = c.Name,
                NormalizedKey = _normalizer.Normalize(c.Name),
                ReferenceCount = count
            };
        }).ToList();

        return new DedupSnapshot(
            candidates,
            recipeIngredientCounts,
            inventoryItemCounts,
            shoppingListItemCounts,
            seasonalityWindowCounts,
            consumeAuditCounts);
    }

    /// <summary>
    /// Walks the fold plan without writing and tallies exact per-table FK
    /// counts that would be reassigned. Uses the snapshot dicts so the
    /// dry-run summary matches what a live run would report.
    /// </summary>
    private static void ProjectSummaryForDryRun(
        IReadOnlyList<FoldGroup> foldGroups,
        DedupSnapshot snapshot,
        DedupSummary summary)
    {
        foreach (var group in foldGroups)
        {
            foreach (var loser in group.Losers)
            {
                summary.AliasRowsWritten++;
                summary.LoserRowsDeleted++;
                summary.RecipeIngredientFksReassigned += snapshot.RecipeIngredientCounts.GetValueOrDefault(loser.Id);
                summary.InventoryItemFksReassigned += snapshot.InventoryItemCounts.GetValueOrDefault(loser.Id);
                summary.ShoppingListItemFksReassigned += snapshot.ShoppingListItemCounts.GetValueOrDefault(loser.Id);
                summary.SeasonalityWindowFksReassigned += snapshot.SeasonalityWindowCounts.GetValueOrDefault(loser.Id);
                summary.ConsumeAuditEntryFksReassigned += snapshot.ConsumeAuditCounts.GetValueOrDefault(loser.Id);
            }
        }
    }

    private sealed record DedupSnapshot(
        IReadOnlyList<CanonicalIngredientFoldCandidate> Candidates,
        IReadOnlyDictionary<Guid, int> RecipeIngredientCounts,
        IReadOnlyDictionary<Guid, int> InventoryItemCounts,
        IReadOnlyDictionary<Guid, int> ShoppingListItemCounts,
        IReadOnlyDictionary<Guid, int> SeasonalityWindowCounts,
        IReadOnlyDictionary<Guid, int> ConsumeAuditCounts);

    /// <summary>
    /// Applies one batch of fold groups in a single transaction. For each
    /// loser inside the batch: insert an alias row pointing at the survivor,
    /// reassign every child-table FK, then delete the loser canonical.
    /// </summary>
    private static async Task ApplyBatchAsync(
        MealsEnPlaceDbContext dbContext,
        IReadOnlyList<FoldGroup> batch,
        DedupSummary summary,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var group in batch)
        {
            foreach (var loser in group.Losers)
            {
                dbContext.CanonicalIngredientAliases.Add(new CanonicalIngredientAlias
                {
                    Alias = loser.Name,
                    CanonicalIngredientId = group.Survivor.Id,
                    CreatedAt = now,
                    Id = Guid.NewGuid()
                });
                summary.AliasRowsWritten++;

                summary.RecipeIngredientFksReassigned += await dbContext.RecipeIngredients
                    .Where(r => r.CanonicalIngredientId == loser.Id)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(r => r.CanonicalIngredientId, group.Survivor.Id),
                        cancellationToken);

                summary.InventoryItemFksReassigned += await dbContext.InventoryItems
                    .Where(r => r.CanonicalIngredientId == loser.Id)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(r => r.CanonicalIngredientId, group.Survivor.Id),
                        cancellationToken);

                summary.ShoppingListItemFksReassigned += await dbContext.ShoppingListItems
                    .Where(r => r.CanonicalIngredientId == loser.Id)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(r => r.CanonicalIngredientId, group.Survivor.Id),
                        cancellationToken);

                summary.SeasonalityWindowFksReassigned += await dbContext.SeasonalityWindows
                    .Where(r => r.CanonicalIngredientId == loser.Id)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(r => r.CanonicalIngredientId, group.Survivor.Id),
                        cancellationToken);

                summary.ConsumeAuditEntryFksReassigned += await dbContext.ConsumeAuditEntries
                    .Where(r => r.CanonicalIngredientId == loser.Id)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(r => r.CanonicalIngredientId, group.Survivor.Id),
                        cancellationToken);

                summary.LoserRowsDeleted += await dbContext.CanonicalIngredients
                    .Where(c => c.Id == loser.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
