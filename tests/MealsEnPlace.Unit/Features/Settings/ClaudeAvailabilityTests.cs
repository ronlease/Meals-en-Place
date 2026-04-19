// Feature: Settings — Claude Availability Gate
//
// Scenario: IsConfiguredAsync returns true when the store returns a non-empty token
// Scenario: IsConfiguredAsync returns false when the store returns null
// Scenario: IsConfiguredAsync returns false when the store returns whitespace

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using Moq;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class ClaudeAvailabilityTests
{
    [Fact]
    public async Task IsConfiguredAsync_WithPopulatedToken_ReturnsTrue()
    {
        // Arrange
        var storeMock = new Mock<IClaudeTokenStore>();
        storeMock.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync("sk-ant-abc");
        var sut = new ClaudeAvailability(storeMock.Object);

        // Act
        var result = await sut.IsConfiguredAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConfiguredAsync_WithNullToken_ReturnsFalse()
    {
        // Arrange
        var storeMock = new Mock<IClaudeTokenStore>();
        storeMock.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var sut = new ClaudeAvailability(storeMock.Object);

        // Act
        var result = await sut.IsConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_WithWhitespaceToken_ReturnsFalse()
    {
        // Arrange
        var storeMock = new Mock<IClaudeTokenStore>();
        storeMock.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync("   ");
        var sut = new ClaudeAvailability(storeMock.Object);

        // Act
        var result = await sut.IsConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }
}
