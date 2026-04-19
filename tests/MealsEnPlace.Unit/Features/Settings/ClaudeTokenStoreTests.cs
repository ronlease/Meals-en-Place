// Feature: Settings — BYO Claude API Key Storage
//
// Scenario: WriteAsync persists a token that ReadAsync returns
//   Given a fresh ClaudeTokenStore backed by a temp directory
//   When WriteAsync is called with a sample token followed by ReadAsync
//   Then the read value equals the written value
//
// Scenario: HasTokenAsync reflects the presence of a token
//   Given a fresh store
//   When WriteAsync is called and then HasTokenAsync
//   Then HasTokenAsync returns true; after ClearAsync it returns false
//
// Scenario: ReadAsync returns null when no token has been persisted
//   Given a fresh store
//   When ReadAsync is called before any WriteAsync
//   Then the returned value is null
//
// Scenario: ClearAsync is safe when no token file exists
//   Given a fresh store with no token file
//   When ClearAsync is called
//   Then no exception is thrown
//
// Scenario: Corrupted token file returns null instead of throwing
//   Given a token file whose contents are not valid protected ciphertext
//   When ReadAsync is called
//   Then the result is null (treated as "no token configured")

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class ClaudeTokenStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ClaudeTokenStore _sut;

    public ClaudeTokenStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mep-tokenstore-" + Guid.NewGuid());
        var keyRingDirectory = Path.Combine(_tempDirectory, "keys");
        Directory.CreateDirectory(keyRingDirectory);

        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("MealsEnPlace.Tests")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingDirectory));
        var provider = services.BuildServiceProvider();

        var options = new ClaudeTokenStoreOptions
        {
            KeyRingDirectory = keyRingDirectory,
            TokenFilePath = Path.Combine(_tempDirectory, "claude-token.dat")
        };
        _sut = new ClaudeTokenStore(provider.GetRequiredService<IDataProtectionProvider>(), options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_ReturnsOriginalToken()
    {
        // Arrange
        const string token = "sk-ant-test-token-abcdef1234567890";

        // Act
        await _sut.WriteAsync(token);
        var roundtrip = await _sut.ReadAsync();

        // Assert
        roundtrip.Should().Be(token);
    }

    [Fact]
    public async Task HasTokenAsync_ReflectsWriteAndClear()
    {
        // Arrange — fresh store starts empty
        (await _sut.HasTokenAsync()).Should().BeFalse();

        // Act — write one
        await _sut.WriteAsync("sk-ant-abc");
        (await _sut.HasTokenAsync()).Should().BeTrue();

        // Act — clear it
        await _sut.ClearAsync();
        (await _sut.HasTokenAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_WithNoPersistedToken_ReturnsNull()
    {
        // Act
        var result = await _sut.ReadAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_WithNoPersistedToken_DoesNotThrow()
    {
        // Act
        var act = async () => await _sut.ClearAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_WithCorruptedFile_ReturnsNullInsteadOfThrowing()
    {
        // Arrange — drop non-protected bytes into the expected path
        var tokenFile = Path.Combine(_tempDirectory, "claude-token.dat");
        await File.WriteAllBytesAsync(tokenFile, [0x00, 0x01, 0x02, 0x03]);

        // Act
        var result = await _sut.ReadAsync();

        // Assert
        result.Should().BeNull();
    }
}
