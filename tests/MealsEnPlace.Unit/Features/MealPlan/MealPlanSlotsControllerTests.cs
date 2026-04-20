// Feature: Meal Plan Slots Controller — consume / unconsume endpoints (MEP-027 / MEP-031)
//
// Scenario: POST /consume on a known slot returns 200 with ConsumeMealResponse
// Scenario: POST /consume on an unknown slot returns 404
// Scenario: DELETE /consume on a known slot returns 204 NoContent
// Scenario: DELETE /consume on an unknown slot returns 404

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.MealPlan;

public sealed class MealPlanSlotsControllerTests
{
    private readonly Mock<IMealConsumptionService> _consumptionMock = new(MockBehavior.Strict);
    private readonly MealPlanSlotsController _sut;

    public MealPlanSlotsControllerTests()
    {
        _sut = new MealPlanSlotsController(_consumptionMock.Object);
    }

    [Fact]
    public async Task Consume_KnownSlot_Returns200WithResponse()
    {
        // Arrange
        var slotId = Guid.NewGuid();
        var consumedAt = DateTime.UtcNow;
        _consumptionMock
            .Setup(c => c.ConsumeAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsumeMealResult
            {
                AutoDepleteApplied = true,
                ConsumedAt = consumedAt,
                ShortIngredients =
                [
                    new ShortIngredient
                    {
                        IngredientName = "Flour",
                        ShortBy = 200m,
                        UnitOfMeasureAbbreviation = "g"
                    }
                ]
            });

        // Act
        var result = await _sut.Consume(slotId);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ConsumeMealResponse>().Subject;
        body.AutoDepleteApplied.Should().BeTrue();
        body.ConsumedAt.Should().Be(consumedAt);
        body.ShortIngredients.Should().HaveCount(1);
        body.ShortIngredients[0].IngredientName.Should().Be("Flour");
    }

    [Fact]
    public async Task Consume_UnknownSlot_Returns404()
    {
        // Arrange
        var slotId = Guid.NewGuid();
        _consumptionMock
            .Setup(c => c.ConsumeAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsumeMealResult?)null);

        // Act
        var result = await _sut.Consume(slotId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Unconsume_KnownSlot_Returns204()
    {
        // Arrange
        var slotId = Guid.NewGuid();
        _consumptionMock
            .Setup(c => c.UnconsumeAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.Unconsume(slotId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Unconsume_UnknownSlot_Returns404()
    {
        // Arrange
        var slotId = Guid.NewGuid();
        _consumptionMock
            .Setup(c => c.UnconsumeAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.Unconsume(slotId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
