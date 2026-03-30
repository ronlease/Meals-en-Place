// Feature: User Preferences Controller
//
// Scenario: Get preferences — returns 200 with Imperial default when no row exists
//   Given no UserPreferences row exists in the database
//   When GET /api/v1/preferences is called
//   Then the response is 200 OK
//   And DisplaySystem is "Imperial"
//
// Scenario: Get preferences — returns 200 with saved preference when row exists
//   Given a UserPreferences row with DisplaySystem = Metric exists
//   When GET /api/v1/preferences is called
//   Then the response is 200 OK
//   And DisplaySystem is "Metric"
//
// Scenario: Update preferences — invalid display system returns validation problem result
//   Given a request with an unrecognized DisplaySystem value
//   When PUT /api/v1/preferences is called
//   Then the response is a validation problem (400 Bad Request)
//
// Scenario: Update preferences — creates new row when none exists
//   Given no UserPreferences row exists
//   When PUT /api/v1/preferences is called with DisplaySystem = "Metric"
//   Then the response is 200 OK
//   And a new UserPreferences row is persisted with DisplaySystem = Metric
//
// Scenario: Update preferences — updates existing row when one exists
//   Given a UserPreferences row with DisplaySystem = Imperial exists
//   When PUT /api/v1/preferences is called with DisplaySystem = "Metric"
//   Then the response is 200 OK
//   And the existing row is updated to DisplaySystem = Metric

using FluentAssertions;
using MealsEnPlace.Api.Features.Preferences;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.Preferences;

public class UserPreferencesControllerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly UserPreferencesController _sut;

    public UserPreferencesControllerTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        _sut = new UserPreferencesController(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoPreferencesRowExists_Returns200WithImperialDefault()
    {
        // Act
        var result = await _sut.Get(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<UserPreferencesResponse>().Subject;
        response.DisplaySystem.Should().Be("Imperial");
    }

    [Fact]
    public async Task Get_PreferencesRowExistsWithMetric_Returns200WithMetric()
    {
        // Arrange
        await SeedPreferencesAsync(DisplaySystem.Metric);

        // Act
        var result = await _sut.Get(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<UserPreferencesResponse>().Subject;
        response.DisplaySystem.Should().Be("Metric");
    }

    [Fact]
    public async Task Get_PreferencesRowExistsWithImperial_Returns200WithImperial()
    {
        // Arrange
        await SeedPreferencesAsync(DisplaySystem.Imperial);

        // Act
        var result = await _sut.Get(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<UserPreferencesResponse>().Subject;
        response.DisplaySystem.Should().Be("Imperial");
    }

    // ── Update — invalid display system ───────────────────────────────────────

    [Fact]
    public async Task Update_InvalidDisplaySystem_ReturnsValidationProblemResult()
    {
        // Arrange
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Fahrenheit" };

        // Act
        var result = await _sut.Update(request, CancellationToken.None);

        // Assert — ValidationProblem returns BadRequestObjectResult (400) when no HttpContext problem factory
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Subject.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Update_InvalidDisplaySystem_DoesNotPersistAnything()
    {
        // Arrange
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Fahrenheit" };

        // Act
        await _sut.Update(request, CancellationToken.None);

        // Assert
        var count = await _dbContext.UserPreferences.CountAsync();
        count.Should().Be(0);
    }

    // ── Update — creates new row ───────────────────────────────────────────────

    [Fact]
    public async Task Update_NoExistingRow_CreatesNewRow()
    {
        // Arrange
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Metric" };

        // Act
        var result = await _sut.Update(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);

        var savedRow = await _dbContext.UserPreferences.FirstOrDefaultAsync();
        savedRow.Should().NotBeNull();
        savedRow!.DisplaySystem.Should().Be(DisplaySystem.Metric);
    }

    [Fact]
    public async Task Update_NoExistingRow_ResponseContainsNewDisplaySystem()
    {
        // Arrange
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Metric" };

        // Act
        var result = await _sut.Update(request, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<UserPreferencesResponse>().Subject;
        response.DisplaySystem.Should().Be("Metric");
    }

    // ── Update — updates existing row ─────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingImperialRow_UpdatesToMetric()
    {
        // Arrange
        await SeedPreferencesAsync(DisplaySystem.Imperial);
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Metric" };

        // Act
        var result = await _sut.Update(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);

        var savedRow = await _dbContext.UserPreferences.FirstOrDefaultAsync();
        savedRow.Should().NotBeNull();
        savedRow!.DisplaySystem.Should().Be(DisplaySystem.Metric);
    }

    [Fact]
    public async Task Update_ExistingRow_DoesNotCreateAdditionalRows()
    {
        // Arrange
        await SeedPreferencesAsync(DisplaySystem.Imperial);
        var request = new UpdateUserPreferencesRequest { DisplaySystem = "Metric" };

        // Act
        await _sut.Update(request, CancellationToken.None);

        // Assert — still only one row
        var count = await _dbContext.UserPreferences.CountAsync();
        count.Should().Be(1);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task SeedPreferencesAsync(DisplaySystem displaySystem)
    {
        var prefs = new UserPreferences
        {
            DisplaySystem = displaySystem,
            Id = new Guid("d1000000-0000-0000-0000-000000000001")
        };
        _dbContext.UserPreferences.Add(prefs);
        await _dbContext.SaveChangesAsync();
    }
}
