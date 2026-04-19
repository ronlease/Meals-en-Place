// Feature: InMemoryUnitOfMeasureResolver
//
// Scenario: Abbreviation match resolves without queueing
//   Given seeded UOMs including "cup"
//   When NormalizeOrDefer("2 cups", ...) is called
//   Then the result resolves to Cup with quantity 2
//   And WasDeferred is false
//   And no UnresolvedUnitOfMeasureToken row is written to the DbContext
//
// Scenario: Alias match resolves without queueing
//   Given a "c." alias pointing to Cup
//   When NormalizeOrDefer("1 c.", ...) is called
//   Then the result resolves to Cup with quantity 1
//
// Scenario: Count-noun fallback resolves to Each
//   Given quantity > 0 but no unit / alias match
//   When NormalizeOrDefer is called
//   Then the result resolves to Each without queueing
//
// Scenario: Unresolved token with no quantity writes a new queue row
//   Given measure "a smidge" (quantity 0, token "a smidge")
//   When NormalizeOrDefer is called
//   Then WasDeferred is true
//   And a new UnresolvedUnitOfMeasureToken row is added to the DbContext with Count = 1
//
// Scenario: Repeat occurrence of the same unresolved token increments an existing row
//   Given the same "a smidge" called twice
//   When NormalizeOrDefer is called twice in the same batch
//   Then a single row exists with Count = 2
//   And SampleIngredientContext reflects the latest caller
//
// Scenario: Empty unit token does not write a queue row
//   Given a measure string with no remainder (e.g. "5")
//   When NormalizeOrDefer is called
//   Then no queue row is written
//
// Scenario: ResetPerBatchState clears the per-batch dedup map
//   Given NormalizeOrDefer was called in batch 1, ResetPerBatchState cleared, then ChangeTracker.Clear, then a new instance
//   When NormalizeOrDefer is called again with the same unresolved token
//   Then the resolver re-queries the DB for the existing row and increments it

using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class InMemoryUnitOfMeasureResolverTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;

    public InMemoryUnitOfMeasureResolverTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);

        // Seed a subset of UOMs sufficient for the test scenarios.
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.EachId,
                Name = "Each",
                UomType = UomType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "cup",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 236.588m,
                Id = UnitOfMeasureConfiguration.CupId,
                Name = "Cup",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.MlId,
                Name = "Milliliter",
                UomType = UomType.Volume
            });

        _dbContext.UnitOfMeasureAliases.Add(new UnitOfMeasureAlias
        {
            Alias = "c.",
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UnitOfMeasureId = UnitOfMeasureConfiguration.CupId
        });

        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Deterministic resolution paths ────────────────────────────────────────

    [Fact]
    public async Task NormalizeOrDefer_AbbreviationMatch_ResolvesWithoutQueueing()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("2 cups", "flour");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");

        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeOrDefer_AliasMatch_ResolvesWithoutQueueing()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("1 c.", "flour");

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
    }

    [Fact]
    public async Task NormalizeOrDefer_CountNounFallback_ResolvesToEach()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("4 chicken breasts", "chicken");

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.Quantity.Should().Be(4m);
    }

    // ── Deferral paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task NormalizeOrDefer_UnresolvedToken_WritesNewQueueRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("a smidge", "cayenne");

        result.WasDeferred.Should().BeTrue();
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().HaveCount(1);

        var queueRow = _dbContext.UnresolvedUnitOfMeasureTokens.Local.First();
        queueRow.UnitToken.Should().Be("a smidge");
        queueRow.Count.Should().Be(1);
        queueRow.SampleIngredientContext.Should().Be("cayenne");
        queueRow.SampleMeasureString.Should().Be("a smidge");
    }

    [Fact]
    public async Task NormalizeOrDefer_RepeatOccurrenceInSameBatch_IncrementsExistingRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        resolver.NormalizeOrDefer("a smidge", "cayenne");
        resolver.NormalizeOrDefer("a smidge", "pepper");

        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().HaveCount(1);

        var queueRow = _dbContext.UnresolvedUnitOfMeasureTokens.Local.First();
        queueRow.Count.Should().Be(2);
        queueRow.SampleIngredientContext.Should().Be("pepper");
    }

    [Fact]
    public async Task NormalizeOrDefer_EmptyUnitToken_DoesNotWriteQueueRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("5", "eggs");

        result.WasDeferred.Should().BeTrue();
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    // ── Per-batch state reset ─────────────────────────────────────────────────

    [Fact]
    public async Task ResetPerBatchState_AfterChangeTrackerClear_ReQueriesDbForExistingRow()
    {
        // Pre-seed the DB with an existing queue row (as if a prior batch wrote it).
        _dbContext.UnresolvedUnitOfMeasureTokens.Add(new UnresolvedUnitOfMeasureToken
        {
            Count = 1,
            FirstSeenAt = DateTime.UtcNow.AddHours(-1),
            Id = Guid.NewGuid(),
            LastSeenAt = DateTime.UtcNow.AddHours(-1),
            SampleIngredientContext = "(old)",
            SampleMeasureString = "a smidge",
            UnitToken = "a smidge"
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);
        resolver.ResetPerBatchState();

        // New batch: same token arrives
        resolver.NormalizeOrDefer("a smidge", "fresh cayenne");

        var rows = await _dbContext.UnresolvedUnitOfMeasureTokens.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Count.Should().Be(2);
        rows[0].SampleIngredientContext.Should().Be("fresh cayenne");
    }
}
