// Feature: UOM Review Queue Controller
//
// Scenario: List returns 200 with queue rows ordered by Count then LastSeenAt descending
//   Given three UnresolvedUnitOfMeasureToken rows with varying counts and timestamps
//   When GET /api/v1/uom-review-queue is called
//   Then the response is 200 OK
//   And the rows are ordered by Count desc, LastSeenAt desc
//
// Scenario: Map returns 404 when the queue row does not exist
//   Given no UnresolvedUnitOfMeasureToken row with the given id
//   When POST /api/v1/uom-review-queue/{id}/map is called
//   Then the response is 404 Not Found
//
// Scenario: Map returns 404 when the target UOM does not exist
//   Given a valid queue row but a non-existent target UnitOfMeasure id
//   When POST /api/v1/uom-review-queue/{id}/map is called
//   Then the response is 404 Not Found
//
// Scenario: Map succeeds for a unique alias
//   Given a queue row with UnitToken "smidge" and no existing alias with that text
//   When POST .../map is called with a valid UnitOfMeasureId
//   Then the response is 200 OK
//   And a new UnitOfMeasureAlias row is inserted
//   And the queue row is deleted
//
// Scenario: Map returns 409 when an alias with the same text already exists and override is off
//   Given a queue row with UnitToken "t" and an existing alias "t" already mapped to Teaspoon
//   When POST .../map is called with UnitOfMeasureId = Tablespoon and allowDuplicateAlias = false
//   Then the response is 409 Conflict
//   And no new alias is inserted
//   And the queue row is NOT deleted
//
// Scenario: Map succeeds when the duplicate override is set
//   Given a queue row with UnitToken "T" and an existing alias "t" (different case) for Teaspoon
//   When POST .../map is called with UnitOfMeasureId = Tablespoon and allowDuplicateAlias = true
//   Then the response is 200 OK
//   And a second alias row with Alias = "T" is inserted
//   And the queue row is deleted
//
// Scenario: Ignore returns 404 when the queue row does not exist
//   When POST /api/v1/uom-review-queue/{id}/ignore is called with an unknown id
//   Then the response is 404 Not Found
//
// Scenario: Ignore deletes the queue row and returns 204
//   Given a queue row
//   When POST .../ignore is called
//   Then the response is 204 No Content
//   And the queue row is removed
//   And no UnitOfMeasureAlias is created

using FluentAssertions;
using MealsEnPlace.Api.Features.UnitOfMeasureReviewQueue;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.UnitOfMeasureReviewQueue;

public class UnitOfMeasureReviewQueueControllerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly UnitOfMeasureReviewQueueController _sut;

    public UnitOfMeasureReviewQueueControllerTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        SeedUoms();
        _sut = new UnitOfMeasureReviewQueueController(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedUoms()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "tsp",
                ConversionFactor = 4.929m,
                Id = UnitOfMeasureConfiguration.TspId,
                Name = "Teaspoon",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tbsp",
                ConversionFactor = 14.787m,
                Id = UnitOfMeasureConfiguration.TbspId,
                Name = "Tablespoon",
                UomType = UomType.Volume
            });
        _dbContext.SaveChanges();
    }

    private UnresolvedUnitOfMeasureToken AddQueueRow(string unitToken, string sampleContext = "pepper", int count = 1)
    {
        var row = new UnresolvedUnitOfMeasureToken
        {
            Count = count,
            FirstSeenAt = DateTime.UtcNow.AddDays(-1),
            Id = Guid.NewGuid(),
            LastSeenAt = DateTime.UtcNow,
            SampleIngredientContext = sampleContext,
            SampleMeasureString = $"1 {unitToken}",
            UnitToken = unitToken
        };
        _dbContext.UnresolvedUnitOfMeasureTokens.Add(row);
        _dbContext.SaveChanges();
        return row;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsRowsOrderedByCountDescendingThenLastSeenAtDescending()
    {
        // Arrange
        AddQueueRow("dash", count: 1);
        AddQueueRow("smidge", count: 5);
        AddQueueRow("pinch", count: 3);

        // Act
        var result = await _sut.List();

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<IEnumerable<UnresolvedUnitOfMeasureTokenResponse>>().Subject.ToList();

        body.Should().HaveCount(3);
        body[0].UnitToken.Should().Be("smidge");
        body[1].UnitToken.Should().Be("pinch");
        body[2].UnitToken.Should().Be("dash");
    }

    // ── Map — not found paths ────────────────────────────────────────────────

    [Fact]
    public async Task Map_QueueRowDoesNotExist_Returns404()
    {
        // Act
        var result = await _sut.Map(
            Guid.NewGuid(),
            new MapTokenToUnitOfMeasureRequest { UnitOfMeasureId = UnitOfMeasureConfiguration.TspId });

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Map_TargetUomDoesNotExist_Returns404()
    {
        // Arrange
        var row = AddQueueRow("smidge");

        // Act
        var result = await _sut.Map(
            row.Id,
            new MapTokenToUnitOfMeasureRequest { UnitOfMeasureId = Guid.NewGuid() });

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Map — success path ───────────────────────────────────────────────────

    [Fact]
    public async Task Map_UniqueAlias_CreatesAliasAndDeletesQueueRow()
    {
        // Arrange
        var row = AddQueueRow("smidge", sampleContext: "cayenne");

        // Act
        var result = await _sut.Map(
            row.Id,
            new MapTokenToUnitOfMeasureRequest { UnitOfMeasureId = UnitOfMeasureConfiguration.TspId });

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<MapTokenToUnitOfMeasureResponse>().Subject;

        body.AliasText.Should().Be("smidge");
        body.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.TspId);
        body.AliasId.Should().NotBe(Guid.Empty);

        var aliasCount = await _dbContext.UnitOfMeasureAliases.CountAsync();
        aliasCount.Should().Be(1);

        var queueRemaining = await _dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueRemaining.Should().Be(0);
    }

    // ── Map — duplicate-alias conflict paths ─────────────────────────────────

    [Fact]
    public async Task Map_DuplicateAliasWithoutOverride_Returns409AndDoesNotDeleteQueueRow()
    {
        // Arrange — existing "t" alias for Teaspoon
        _dbContext.UnitOfMeasureAliases.Add(new UnitOfMeasureAlias
        {
            Alias = "t",
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UnitOfMeasureId = UnitOfMeasureConfiguration.TspId
        });
        _dbContext.SaveChanges();

        var row = AddQueueRow("t");

        // Act — attempt to map to Tablespoon with same alias text, no override
        var result = await _sut.Map(
            row.Id,
            new MapTokenToUnitOfMeasureRequest { UnitOfMeasureId = UnitOfMeasureConfiguration.TbspId });

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();

        var aliasCount = await _dbContext.UnitOfMeasureAliases.CountAsync();
        aliasCount.Should().Be(1); // no new alias

        var queueRemaining = await _dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueRemaining.Should().Be(1); // row NOT deleted
    }

    [Fact]
    public async Task Map_DuplicateAliasWithOverride_CreatesSecondAliasAndDeletesQueueRow()
    {
        // Arrange — existing "t" alias for Teaspoon; new row is for "T" (different case, same string compare)
        _dbContext.UnitOfMeasureAliases.Add(new UnitOfMeasureAlias
        {
            Alias = "t",
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UnitOfMeasureId = UnitOfMeasureConfiguration.TspId
        });
        _dbContext.SaveChanges();

        // Use exact-match "t" so we can exercise the override path with a predictable conflict
        var row = AddQueueRow("t");

        // Act — override enabled
        var result = await _sut.Map(
            row.Id,
            new MapTokenToUnitOfMeasureRequest
            {
                AllowDuplicateAlias = true,
                UnitOfMeasureId = UnitOfMeasureConfiguration.TbspId
            });

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<MapTokenToUnitOfMeasureResponse>().Subject;

        body.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.TbspId);

        var aliasCount = await _dbContext.UnitOfMeasureAliases.CountAsync();
        aliasCount.Should().Be(2);

        var queueRemaining = await _dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueRemaining.Should().Be(0);
    }

    // ── Ignore ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ignore_QueueRowDoesNotExist_Returns404()
    {
        // Act
        var result = await _sut.Ignore(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Ignore_DeletesQueueRowAndDoesNotCreateAlias()
    {
        // Arrange
        var row = AddQueueRow("garbage");

        // Act
        var result = await _sut.Ignore(row.Id);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        var queueRemaining = await _dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueRemaining.Should().Be(0);

        var aliasCount = await _dbContext.UnitOfMeasureAliases.CountAsync();
        aliasCount.Should().Be(0);
    }
}
