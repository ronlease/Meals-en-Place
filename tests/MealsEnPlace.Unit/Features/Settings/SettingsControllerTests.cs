// Feature: Settings — BYO Claude API Key Controller
//
// Scenario: GET /status returns configured=true when a token is stored
// Scenario: GET /status returns configured=false when no token is stored
// Scenario: POST /token persists the value and response omits the raw key
// Scenario: POST /token rejects empty/whitespace tokens with 400
// Scenario: POST /test uses the candidate token when one is supplied
// Scenario: POST /test falls back to the persisted token when the request body omits one
// Scenario: POST /test with no persisted and no candidate token returns 400
// Scenario: POST /test does not overwrite the persisted token on failure
// Scenario: DELETE /token removes any persisted value

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class SettingsControllerTests
{
    private readonly Mock<IAnthropicTestClient> _anthropicMock = new(MockBehavior.Strict);
    private readonly FakeClaudeTokenStore _store = new();
    private readonly SettingsController _sut;
    private readonly TodoistOptions _todoistOptions = new();

    public SettingsControllerTests()
    {
        _sut = new SettingsController(
            _anthropicMock.Object,
            _store,
            Options.Create(_todoistOptions));
    }

    [Fact]
    public async Task GetClaudeStatus_WithStoredToken_ReturnsConfiguredTrue()
    {
        // Arrange
        await _store.WriteAsync("sk-ant-existing");

        // Act
        var action = await _sut.GetClaudeStatus();

        // Assert
        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeTrue();
    }

    [Fact]
    public async Task GetClaudeStatus_WithoutStoredToken_ReturnsConfiguredFalse()
    {
        // Act
        var action = await _sut.GetClaudeStatus();

        // Assert
        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task SaveClaudeToken_PersistsValue_AndResponseOmitsRawKey()
    {
        // Arrange
        var request = new SaveClaudeTokenRequest { Token = "sk-ant-newly-issued" };

        // Act
        var action = await _sut.SaveClaudeToken(request);

        // Assert — persisted
        (await _store.ReadAsync()).Should().Be("sk-ant-newly-issued");

        // Assert — response body never contains the raw token
        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeTrue();

        var serialized = System.Text.Json.JsonSerializer.Serialize(body);
        serialized.Should().NotContain("sk-ant-newly-issued");
    }

    [Fact]
    public async Task SaveClaudeToken_WithWhitespace_Returns400()
    {
        // Act
        var action = await _sut.SaveClaudeToken(new SaveClaudeTokenRequest { Token = "   " });

        // Assert
        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestClaudeToken_UsesCandidateWhenProvided()
    {
        // Arrange
        await _store.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-candidate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { Success = true });

        // Act
        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = "sk-ant-candidate" });

        // Assert
        GetBody<ClaudeTokenTestResponse>(action).Success.Should().BeTrue();
        _anthropicMock.Verify(a => a.PingAsync("sk-ant-candidate", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestClaudeToken_FallsBackToPersistedTokenWhenCandidateOmitted()
    {
        // Arrange
        await _store.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-persisted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { Success = true });

        // Act
        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = null });

        // Assert
        GetBody<ClaudeTokenTestResponse>(action).Success.Should().BeTrue();
        _anthropicMock.Verify(a => a.PingAsync("sk-ant-persisted", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestClaudeToken_WithNoPersistedAndNoCandidate_Returns400()
    {
        // Act
        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = null });

        // Assert
        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestClaudeToken_FailedCandidate_DoesNotOverwritePersistedToken()
    {
        // Arrange
        await _store.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { ErrorMessage = "invalid_api_key", Success = false });

        // Act
        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = "sk-ant-bad" });

        // Assert — failure surfaced
        var body = GetBody<ClaudeTokenTestResponse>(action);
        body.Success.Should().BeFalse();
        body.ErrorMessage.Should().Be("invalid_api_key");

        // Assert — persisted token unchanged
        (await _store.ReadAsync()).Should().Be("sk-ant-persisted");
    }

    [Fact]
    public async Task ClearClaudeToken_RemovesAnyPersistedValue()
    {
        // Arrange
        await _store.WriteAsync("sk-ant-to-go");

        // Act
        var action = await _sut.ClearClaudeToken();

        // Assert
        GetBody<ClaudeTokenStatusResponse>(action).Configured.Should().BeFalse();
        (await _store.ReadAsync()).Should().BeNull();
    }

    [Fact]
    public void GetTodoistStatus_WhenTokenPresent_ReportsConfiguredTrue()
    {
        // Arrange
        _todoistOptions.Token = "sample-token";

        // Act
        var action = _sut.GetTodoistStatus();

        // Assert
        GetBody<TodoistStatusResponse>(action).Configured.Should().BeTrue();
    }

    [Fact]
    public void GetTodoistStatus_WhenTokenAbsent_ReportsConfiguredFalse()
    {
        // Act — _todoistOptions.Token stays null
        var action = _sut.GetTodoistStatus();

        // Assert
        GetBody<TodoistStatusResponse>(action).Configured.Should().BeFalse();
    }

    private static T GetBody<T>(ActionResult<T> action) where T : class
    {
        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<T>().Subject;
    }

    /// <summary>In-memory <see cref="IClaudeTokenStore"/> for controller-level tests.</summary>
    private sealed class FakeClaudeTokenStore : IClaudeTokenStore
    {
        private string? _token;

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _token = null;
            return Task.CompletedTask;
        }

        public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrWhiteSpace(_token));

        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_token);

        public Task WriteAsync(string token, CancellationToken cancellationToken = default)
        {
            _token = token;
            return Task.CompletedTask;
        }
    }
}
