// Feature: Todoist token resolution chain (MEP-035)
//
// Scenario: Encrypted store wins when both sources have a token
//   Given the encrypted store holds "from-store" and the user-secret holds "from-secret"
//   When ResolveAsync is called
//   Then the returned value is "from-store"
//
// Scenario: User-secret fallback is returned when the encrypted store is empty
//   Given the encrypted store has no token and the user-secret holds "from-secret"
//   When ResolveAsync is called
//   Then the returned value is "from-secret"
//
// Scenario: Neither source configured yields null
//   Given both sources are empty
//   When ResolveAsync is called
//   Then the returned value is null
//
// Scenario: Whitespace-only store value defers to the fallback
//   Given the store returns "   " and the user-secret holds "from-secret"
//   When ResolveAsync is called
//   Then the returned value is "from-secret"
//
// Scenario: HasTokenAsync reflects the resolved value
//   Given sources that yield a non-empty resolved token
//   When HasTokenAsync is called
//   Then it returns true; the inverse for empty sources

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Unit.Infrastructure.Todoist;

public sealed class TodoistTokenResolverTests
{
    [Fact]
    public async Task ResolveAsync_EncryptedStoreWinsOverUserSecretFallback()
    {
        var store = new FakeStore { Token = "from-store" };
        var options = Options.Create(new TodoistOptions { Token = "from-secret" });
        var sut = new TodoistTokenResolver(options, store);

        var resolved = await sut.ResolveAsync();

        resolved.Should().Be("from-store");
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToUserSecretWhenStoreEmpty()
    {
        var store = new FakeStore { Token = null };
        var options = Options.Create(new TodoistOptions { Token = "from-secret" });
        var sut = new TodoistTokenResolver(options, store);

        var resolved = await sut.ResolveAsync();

        resolved.Should().Be("from-secret");
    }

    [Fact]
    public async Task ResolveAsync_NeitherConfigured_ReturnsNull()
    {
        var store = new FakeStore { Token = null };
        var options = Options.Create(new TodoistOptions { Token = null });
        var sut = new TodoistTokenResolver(options, store);

        var resolved = await sut.ResolveAsync();

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhitespaceStoreDefersToFallback()
    {
        var store = new FakeStore { Token = "   " };
        var options = Options.Create(new TodoistOptions { Token = "from-secret" });
        var sut = new TodoistTokenResolver(options, store);

        var resolved = await sut.ResolveAsync();

        resolved.Should().Be("from-secret");
    }

    [Fact]
    public async Task HasTokenAsync_TrueWhenAnySourceConfigured()
    {
        var store = new FakeStore { Token = null };
        var options = Options.Create(new TodoistOptions { Token = "from-secret" });
        var sut = new TodoistTokenResolver(options, store);

        (await sut.HasTokenAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task HasTokenAsync_FalseWhenNeitherSourceConfigured()
    {
        var store = new FakeStore { Token = null };
        var options = Options.Create(new TodoistOptions { Token = null });
        var sut = new TodoistTokenResolver(options, store);

        (await sut.HasTokenAsync()).Should().BeFalse();
    }

    private sealed class FakeStore : ITodoistTokenStore
    {
        public string? Token { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Token = null;
            return Task.CompletedTask;
        }

        public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrWhiteSpace(Token));

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Token);

        public Task WriteAsync(string token, CancellationToken cancellationToken = default)
        {
            Token = token;
            return Task.CompletedTask;
        }
    }
}
