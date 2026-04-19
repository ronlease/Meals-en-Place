// Feature: Waste Alert Controller
//
// Scenario: Get alerts returns 200 with alert list
//   Given the service returns a list of active waste alerts
//   When GET /api/v1/waste-alerts is called
//   Then the response is 200 OK
//   And the body contains the list of alerts
//
// Scenario: Get alerts returns 200 with empty list when no alerts exist
//   Given the service returns an empty list
//   When GET /api/v1/waste-alerts is called
//   Then the response is 200 OK
//   And the body is an empty list
//
// Scenario: Dismiss alert returns 204 when alert exists
//   Given the service successfully dismisses the alert
//   When POST /api/v1/waste-alerts/{id}/dismiss is called
//   Then the response is 204 No Content
//
// Scenario: Dismiss alert returns 404 when alert does not exist
//   Given the service returns false for the dismiss attempt
//   When POST /api/v1/waste-alerts/{id}/dismiss is called
//   Then the response is 404 Not Found

using FluentAssertions;
using MealsEnPlace.Api.Features.WasteReduction;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.WasteReduction;

public class WasteAlertControllerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly WasteAlertController _sut;
    private readonly Mock<IWasteAlertService> _wasteAlertServiceMock = new(MockBehavior.Strict);

    public WasteAlertControllerTests()
    {
        _sut = new WasteAlertController(_wasteAlertServiceMock.Object);
    }

    private static WasteAlertResponse BuildAlert(Guid? alertId = null) =>
        new()
        {
            AlertId = alertId ?? Guid.NewGuid(),
            CanonicalIngredientName = "Greek Yogurt",
            CreatedAt = DateTime.UtcNow,
            DaysUntilExpiry = 2,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            InventoryItemId = Guid.NewGuid(),
            Location = "Fridge",
            MatchedRecipes = [],
            Quantity = 500m,
            UnitOfMeasureAbbreviation = "g"
        };

    // ── DismissAlert ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DismissAlert_AlertExists_Returns204NoContent()
    {
        // Arrange
        var id = Guid.NewGuid();
        _wasteAlertServiceMock
            .Setup(s => s.DismissAlertAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DismissAlert(id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DismissAlert_AlertDoesNotExist_Returns404NotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _wasteAlertServiceMock
            .Setup(s => s.DismissAlertAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DismissAlert(id);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DismissAlert_AlertExists_CallsServiceOnce()
    {
        // Arrange
        var id = Guid.NewGuid();
        _wasteAlertServiceMock
            .Setup(s => s.DismissAlertAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DismissAlert(id);

        // Assert
        _wasteAlertServiceMock.Verify(s => s.DismissAlertAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetAlerts ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAlerts_ServiceReturnsAlerts_Returns200WithAlertList()
    {
        // Arrange
        var alerts = new List<WasteAlertResponse> { BuildAlert(), BuildAlert() };
        _wasteAlertServiceMock
            .Setup(s => s.EvaluateAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        // Act
        var result = await _sut.GetAlerts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(alerts);
    }

    [Fact]
    public async Task GetAlerts_ServiceReturnsEmptyList_Returns200WithEmptyList()
    {
        // Arrange
        _wasteAlertServiceMock
            .Setup(s => s.EvaluateAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetAlerts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeAssignableTo<List<WasteAlertResponse>>().Subject;
        value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAlerts_CallsEvaluateAlertsAsyncOnce()
    {
        // Arrange
        _wasteAlertServiceMock
            .Setup(s => s.EvaluateAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.GetAlerts();

        // Assert
        _wasteAlertServiceMock.Verify(s => s.EvaluateAlertsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAlerts_ServiceReturnsSingleAlert_ResponseContainsCorrectIngredientName()
    {
        // Arrange
        var alert = BuildAlert();
        _wasteAlertServiceMock
            .Setup(s => s.EvaluateAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);

        // Act
        var result = await _sut.GetAlerts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeAssignableTo<List<WasteAlertResponse>>().Subject;
        value.Should().ContainSingle(a => a.CanonicalIngredientName == "Greek Yogurt");
    }
}
