// Feature: Settings — BYO Todoist API Token Storage (MEP-035)
//
// Scenario: WriteAsync persists a token that ReadAsync returns
//   Given a fresh TodoistTokenStore backed by a temp directory
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
//
// Scenario: Ciphertext is not interchangeable with the Claude token store
//   Given a token written by TodoistTokenStore
//   When the same bytes are handed to a ClaudeTokenStore sharing the same key ring
//   Then ReadAsync on the Claude store returns null (distinct DataProtection purpose)

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class TodoistTokenStoreTests : IDisposable
{
    private readonly string _keyRingDirectory;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly TodoistTokenStore _sut;
    private readonly string _tempDirectory;

    public TodoistTokenStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mep-todoist-tokenstore-" + Guid.NewGuid());
        _keyRingDirectory = Path.Combine(_tempDirectory, "keys");
        Directory.CreateDirectory(_keyRingDirectory);

        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("MealsEnPlace.Tests")
            .PersistKeysToFileSystem(new DirectoryInfo(_keyRingDirectory));
        _protectionProvider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();

        var options = new TodoistTokenStoreOptions
        {
            TokenFilePath = Path.Combine(_tempDirectory, "todoist-token.dat")
        };
        _sut = new TodoistTokenStore(_protectionProvider, options);
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
        const string token = "todoist-test-token-abcdef1234567890";

        await _sut.WriteAsync(token);
        var roundtrip = await _sut.ReadAsync();

        roundtrip.Should().Be(token);
    }

    [Fact]
    public async Task HasTokenAsync_ReflectsWriteAndClear()
    {
        (await _sut.HasTokenAsync()).Should().BeFalse();

        await _sut.WriteAsync("todoist-abc");
        (await _sut.HasTokenAsync()).Should().BeTrue();

        await _sut.ClearAsync();
        (await _sut.HasTokenAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_WithNoPersistedToken_ReturnsNull()
    {
        var result = await _sut.ReadAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_WithNoPersistedToken_DoesNotThrow()
    {
        var act = async () => await _sut.ClearAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_WithCorruptedFile_ReturnsNullInsteadOfThrowing()
    {
        var tokenFile = Path.Combine(_tempDirectory, "todoist-token.dat");
        await File.WriteAllBytesAsync(tokenFile, [0x00, 0x01, 0x02, 0x03]);

        var result = await _sut.ReadAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task Ciphertext_IsNotInterchangeableWithClaudeStore()
    {
        // Arrange — share the key ring with a Claude store, then write via Todoist
        await _sut.WriteAsync("todoist-secret-value");
        var claudeTokenPath = Path.Combine(_tempDirectory, "claude-token.dat");
        File.Copy(Path.Combine(_tempDirectory, "todoist-token.dat"), claudeTokenPath);

        var claudeStore = new ClaudeTokenStore(_protectionProvider, new ClaudeTokenStoreOptions
        {
            KeyRingDirectory = _keyRingDirectory,
            TokenFilePath = claudeTokenPath
        });

        // Act — Claude store attempts to read bytes produced by the Todoist purpose
        var crossRead = await claudeStore.ReadAsync();

        // Assert — distinct DataProtection purposes mean the ciphertext is not valid
        // for the Claude store; it returns null (the corrupted-file fallback) rather
        // than leaking the plaintext across providers.
        crossRead.Should().BeNull();
    }
}
