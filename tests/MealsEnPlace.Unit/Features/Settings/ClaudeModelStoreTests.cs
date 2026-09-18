// Feature: Settings — Claude Model Preference Storage (MEP-052)
//
// Scenario: WriteAsync persists a model that ReadAsync returns
//   Given a fresh ClaudeModelStore backed by a temp directory
//   When WriteAsync is called with a model followed by ReadAsync
//   Then the read value equals the written value
//
// Scenario: ReadAsync returns the catalog default when no preference has been persisted
//   Given a fresh store with no model file
//   When ReadAsync is called
//   Then the result is ClaudeModelCatalog.Default
//
// Scenario: ReadAsync falls back to the default when the persisted file is unrecognized
//   Given a model file containing a value that is not a known ClaudeModel member name
//   When ReadAsync is called
//   Then the result is ClaudeModelCatalog.Default rather than throwing
//
// Scenario: WriteAsync creates the containing directory when it does not yet exist

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class ClaudeModelStoreTests : IDisposable
{
    private readonly ClaudeModelStore _sut;
    private readonly string _tempDirectory;

    public ClaudeModelStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mep-modelstore-" + Guid.NewGuid());
        var options = new ClaudeModelStoreOptions
        {
            ModelFilePath = Path.Combine(_tempDirectory, "claude-model.txt")
        };
        _sut = new ClaudeModelStore(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_ReturnsOriginalModel()
    {
        // Act
        await _sut.WriteAsync(ClaudeModel.Opus5);
        var roundtrip = await _sut.ReadAsync();

        // Assert
        roundtrip.Should().Be(ClaudeModel.Opus5);
    }

    [Fact]
    public async Task ReadAsync_WithNoPersistedModel_ReturnsDefault()
    {
        // Act
        var result = await _sut.ReadAsync();

        // Assert
        result.Should().Be(ClaudeModelCatalog.Default);
    }

    [Fact]
    public async Task ReadAsync_WithUnrecognizedFileContent_ReturnsDefaultInsteadOfThrowing()
    {
        // Arrange — simulates a value from a retired model, or manual tampering
        Directory.CreateDirectory(_tempDirectory);
        var modelFile = Path.Combine(_tempDirectory, "claude-model.txt");
        await File.WriteAllTextAsync(modelFile, "SomeRetiredModel");

        // Act
        var result = await _sut.ReadAsync();

        // Assert
        result.Should().Be(ClaudeModelCatalog.Default);
    }

    [Fact]
    public async Task WriteAsync_CreatesContainingDirectoryWhenMissing()
    {
        // Arrange — the temp directory itself does not exist yet
        Directory.Exists(_tempDirectory).Should().BeFalse();

        // Act
        await _sut.WriteAsync(ClaudeModel.Haiku45);

        // Assert
        (await _sut.ReadAsync()).Should().Be(ClaudeModel.Haiku45);
    }
}
