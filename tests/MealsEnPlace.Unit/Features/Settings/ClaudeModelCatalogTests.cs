// Feature: Claude Model Catalog (MEP-052)
//
// Scenario: AnthropicModelId returns the Anthropic API ID for each selectable model
// Scenario: DisplayName returns a human-readable label for each selectable model
// Scenario: TryParse resolves a member name case-insensitively
// Scenario: TryParse falls back to Default for null, empty, or unrecognized input

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class ClaudeModelCatalogTests
{
    [Theory]
    [InlineData(ClaudeModel.Fable51, "claude-fable-5-1")]
    [InlineData(ClaudeModel.Haiku45, "claude-haiku-4-5-20251001")]
    [InlineData(ClaudeModel.Opus5, "claude-opus-5")]
    [InlineData(ClaudeModel.Sonnet5, "claude-sonnet-5")]
    public void AnthropicModelId_ReturnsExpectedId(ClaudeModel model, string expectedId)
    {
        ClaudeModelCatalog.AnthropicModelId(model).Should().Be(expectedId);
    }

    [Theory]
    [InlineData(ClaudeModel.Fable51, "Fable 5.1")]
    [InlineData(ClaudeModel.Haiku45, "Haiku 4.5")]
    [InlineData(ClaudeModel.Opus5, "Opus 5")]
    [InlineData(ClaudeModel.Sonnet5, "Sonnet 5")]
    public void DisplayName_ReturnsExpectedLabel(ClaudeModel model, string expectedLabel)
    {
        ClaudeModelCatalog.DisplayName(model).Should().Be(expectedLabel);
    }

    [Fact]
    public void Default_IsSonnet5()
    {
        ClaudeModelCatalog.Default.Should().Be(ClaudeModel.Sonnet5);
    }

    [Theory]
    [InlineData("Opus5")]
    [InlineData("opus5")]
    [InlineData("OPUS5")]
    public void TryParse_ResolvesMemberNameCaseInsensitively(string value)
    {
        var result = ClaudeModelCatalog.TryParse(value, out var model);

        result.Should().BeTrue();
        model.Should().Be(ClaudeModel.Opus5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("claude-opus-5")]
    [InlineData("Gpt5")]
    public void TryParse_WithUnrecognizedInput_ReturnsFalseAndDefault(string? value)
    {
        var result = ClaudeModelCatalog.TryParse(value, out var model);

        result.Should().BeFalse();
        model.Should().Be(ClaudeModelCatalog.Default);
    }
}
