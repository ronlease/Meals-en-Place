// Feature: Settings — BYO Claude and Todoist API Key Controller
//
// Claude scenarios:
// Scenario: GET /claude/status returns configured=true when a token is stored
// Scenario: GET /claude/status returns configured=false when no token is stored
// Scenario: GET /claude/status includes the currently selected model
// Scenario: POST /claude/token persists the value and response omits the raw key
// Scenario: POST /claude/token rejects empty/whitespace tokens with 400
// Scenario: POST /claude/token response includes the currently selected model
// Scenario: POST /claude/test uses the candidate token when one is supplied
// Scenario: POST /claude/test falls back to the persisted token when the request body omits one
// Scenario: POST /claude/test with no persisted and no candidate token returns 400
// Scenario: POST /claude/test does not overwrite the persisted token on failure
// Scenario: DELETE /claude/token removes any persisted value but leaves the model preference untouched
//
// Claude model scenarios (MEP-052):
// Scenario: POST /claude/model persists a recognized model and returns it
// Scenario: POST /claude/model rejects an unrecognized model name with 400
// Scenario: POST /claude/model does not require a key to be configured
//
// Todoist scenarios (MEP-035 / MEP-036):
// Scenario: GET /todoist/status reports configured when the resolver returns a token
// Scenario: GET /todoist/status reports not-configured when the resolver is empty
// Scenario: POST /todoist/token persists the value and response omits the raw token
// Scenario: POST /todoist/token rejects empty/whitespace tokens with 400
// Scenario: POST /todoist/test uses the candidate token when one is supplied
// Scenario: POST /todoist/test falls back to the resolved token when the request body omits one
// Scenario: POST /todoist/test with no resolved and no candidate token returns 400
// Scenario: POST /todoist/test does not overwrite the persisted token on failure
// Scenario: DELETE /todoist/token removes any persisted encrypted value
// Scenario: GET /todoist/projects/history returns the service result as 200
// Scenario: GET /todoist/projects/history when Todoist is unreachable returns degraded result with NamesResolved=false

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class SettingsControllerTests
{
    private readonly Mock<IAnthropicTestClient> _anthropicMock = new(MockBehavior.Strict);
    private readonly FakeClaudeModelStore _claudeModelStore = new();
    private readonly FakeClaudeTokenStore _claudeStore = new();
    private readonly Mock<ITodoistProjectHistoryService> _historyServiceMock = new(MockBehavior.Strict);
    private readonly SettingsController _sut;
    private readonly Mock<ITodoistTestClient> _todoistTestMock = new(MockBehavior.Strict);
    private readonly FakeTodoistTokenStore _todoistStore = new();

    public SettingsControllerTests()
    {
        _sut = new SettingsController(
            _anthropicMock.Object,
            _claudeModelStore,
            _claudeStore,
            _historyServiceMock.Object,
            _todoistTestMock.Object,
            new ResolverOverStore(_todoistStore),
            _todoistStore);
    }

    [Fact]
    public async Task GetClaudeStatus_WithStoredToken_ReturnsConfiguredTrue()
    {
        await _claudeStore.WriteAsync("sk-ant-existing");

        var action = await _sut.GetClaudeStatus();

        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeTrue();
    }

    [Fact]
    public async Task GetClaudeStatus_WithoutStoredToken_ReturnsConfiguredFalse()
    {
        var action = await _sut.GetClaudeStatus();

        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task GetClaudeStatus_IncludesCurrentlySelectedModel()
    {
        await _claudeModelStore.WriteAsync(ClaudeModel.Opus5);

        var action = await _sut.GetClaudeStatus();

        GetBody<ClaudeTokenStatusResponse>(action).Model.Should().Be("Opus5");
    }

    [Fact]
    public async Task SaveClaudeToken_PersistsValue_AndResponseOmitsRawKey()
    {
        var request = new SaveClaudeTokenRequest { Token = "sk-ant-newly-issued" };

        var action = await _sut.SaveClaudeToken(request);

        (await _claudeStore.ReadAsync()).Should().Be("sk-ant-newly-issued");
        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Configured.Should().BeTrue();
        System.Text.Json.JsonSerializer.Serialize(body).Should().NotContain("sk-ant-newly-issued");
    }

    [Fact]
    public async Task SaveClaudeToken_ResponseIncludesCurrentlySelectedModel()
    {
        await _claudeModelStore.WriteAsync(ClaudeModel.Haiku45);

        var action = await _sut.SaveClaudeToken(new SaveClaudeTokenRequest { Token = "sk-ant-newly-issued" });

        GetBody<ClaudeTokenStatusResponse>(action).Model.Should().Be("Haiku45");
    }

    [Fact]
    public async Task SaveClaudeToken_WithWhitespace_Returns400()
    {
        var action = await _sut.SaveClaudeToken(new SaveClaudeTokenRequest { Token = "   " });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestClaudeToken_UsesCandidateWhenProvided()
    {
        await _claudeStore.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-candidate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { Success = true });

        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = "sk-ant-candidate" });

        GetBody<ClaudeTokenTestResponse>(action).Success.Should().BeTrue();
        _anthropicMock.Verify(a => a.PingAsync("sk-ant-candidate", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestClaudeToken_FallsBackToPersistedTokenWhenCandidateOmitted()
    {
        await _claudeStore.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-persisted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { Success = true });

        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = null });

        GetBody<ClaudeTokenTestResponse>(action).Success.Should().BeTrue();
        _anthropicMock.Verify(a => a.PingAsync("sk-ant-persisted", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestClaudeToken_WithNoPersistedAndNoCandidate_Returns400()
    {
        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = null });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestClaudeToken_FailedCandidate_DoesNotOverwritePersistedToken()
    {
        await _claudeStore.WriteAsync("sk-ant-persisted");
        _anthropicMock
            .Setup(a => a.PingAsync("sk-ant-bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicTestResult { ErrorMessage = "invalid_api_key", Success = false });

        var action = await _sut.TestClaudeToken(new TestClaudeTokenRequest { Token = "sk-ant-bad" });

        var body = GetBody<ClaudeTokenTestResponse>(action);
        body.Success.Should().BeFalse();
        body.ErrorMessage.Should().Be("invalid_api_key");
        (await _claudeStore.ReadAsync()).Should().Be("sk-ant-persisted");
    }

    [Fact]
    public async Task ClearClaudeToken_RemovesAnyPersistedValue()
    {
        await _claudeStore.WriteAsync("sk-ant-to-go");

        var action = await _sut.ClearClaudeToken();

        GetBody<ClaudeTokenStatusResponse>(action).Configured.Should().BeFalse();
        (await _claudeStore.ReadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ClearClaudeToken_LeavesModelPreferenceUntouched()
    {
        await _claudeStore.WriteAsync("sk-ant-to-go");
        await _claudeModelStore.WriteAsync(ClaudeModel.Opus5);

        var action = await _sut.ClearClaudeToken();

        GetBody<ClaudeTokenStatusResponse>(action).Model.Should().Be("Opus5");
        (await _claudeModelStore.ReadAsync()).Should().Be(ClaudeModel.Opus5);
    }

    [Fact]
    public async Task SaveClaudeModel_PersistsRecognizedModel_AndReturnsIt()
    {
        var action = await _sut.SaveClaudeModel(new SaveClaudeModelRequest { Model = "Opus5" });

        GetBody<ClaudeTokenStatusResponse>(action).Model.Should().Be("Opus5");
        (await _claudeModelStore.ReadAsync()).Should().Be(ClaudeModel.Opus5);
    }

    [Fact]
    public async Task SaveClaudeModel_IsCaseInsensitive()
    {
        var action = await _sut.SaveClaudeModel(new SaveClaudeModelRequest { Model = "haiku45" });

        GetBody<ClaudeTokenStatusResponse>(action).Model.Should().Be("Haiku45");
    }

    [Fact]
    public async Task SaveClaudeModel_WithUnrecognizedName_Returns400_AndDoesNotPersist()
    {
        await _claudeModelStore.WriteAsync(ClaudeModel.Sonnet5);

        var action = await _sut.SaveClaudeModel(new SaveClaudeModelRequest { Model = "Gpt5" });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await _claudeModelStore.ReadAsync()).Should().Be(ClaudeModel.Sonnet5);
    }

    [Fact]
    public async Task SaveClaudeModel_WithNullModel_Returns400()
    {
        var action = await _sut.SaveClaudeModel(new SaveClaudeModelRequest { Model = null });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SaveClaudeModel_DoesNotRequireAConfiguredKey()
    {
        (await _claudeStore.HasTokenAsync()).Should().BeFalse();

        var action = await _sut.SaveClaudeModel(new SaveClaudeModelRequest { Model = "Fable51" });

        var body = GetBody<ClaudeTokenStatusResponse>(action);
        body.Model.Should().Be("Fable51");
        body.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task GetTodoistStatus_WithStoredToken_ReturnsConfiguredTrue()
    {
        await _todoistStore.WriteAsync("todoist-existing");

        var action = await _sut.GetTodoistStatus();

        GetBody<TodoistStatusResponse>(action).Configured.Should().BeTrue();
    }

    [Fact]
    public async Task GetTodoistStatus_WithoutStoredToken_ReturnsConfiguredFalse()
    {
        var action = await _sut.GetTodoistStatus();

        GetBody<TodoistStatusResponse>(action).Configured.Should().BeFalse();
    }

    [Fact]
    public async Task SaveTodoistToken_PersistsValue_AndResponseOmitsRawToken()
    {
        var request = new SaveTodoistTokenRequest { Token = "todoist-newly-issued" };

        var action = await _sut.SaveTodoistToken(request);

        (await _todoistStore.ReadAsync()).Should().Be("todoist-newly-issued");
        var body = GetBody<TodoistStatusResponse>(action);
        body.Configured.Should().BeTrue();
        System.Text.Json.JsonSerializer.Serialize(body).Should().NotContain("todoist-newly-issued");
    }

    [Fact]
    public async Task SaveTodoistToken_WithWhitespace_Returns400()
    {
        var action = await _sut.SaveTodoistToken(new SaveTodoistTokenRequest { Token = "   " });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestTodoistToken_UsesCandidateWhenProvided()
    {
        await _todoistStore.WriteAsync("todoist-persisted");
        _todoistTestMock
            .Setup(c => c.PingAsync("todoist-candidate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistTestResult { Success = true });

        var action = await _sut.TestTodoistToken(new TestTodoistTokenRequest { Token = "todoist-candidate" });

        GetBody<TodoistTokenTestResponse>(action).Success.Should().BeTrue();
        _todoistTestMock.Verify(c => c.PingAsync("todoist-candidate", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestTodoistToken_FallsBackToResolvedTokenWhenCandidateOmitted()
    {
        await _todoistStore.WriteAsync("todoist-persisted");
        _todoistTestMock
            .Setup(c => c.PingAsync("todoist-persisted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistTestResult { Success = true });

        var action = await _sut.TestTodoistToken(new TestTodoistTokenRequest { Token = null });

        GetBody<TodoistTokenTestResponse>(action).Success.Should().BeTrue();
        _todoistTestMock.Verify(c => c.PingAsync("todoist-persisted", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestTodoistToken_WithNoPersistedAndNoCandidate_Returns400()
    {
        var action = await _sut.TestTodoistToken(new TestTodoistTokenRequest { Token = null });

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TestTodoistToken_FailedCandidate_DoesNotOverwritePersistedToken()
    {
        await _todoistStore.WriteAsync("todoist-persisted");
        _todoistTestMock
            .Setup(c => c.PingAsync("todoist-bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistTestResult { ErrorMessage = "Invalid token", Success = false });

        var action = await _sut.TestTodoistToken(new TestTodoistTokenRequest { Token = "todoist-bad" });

        var body = GetBody<TodoistTokenTestResponse>(action);
        body.Success.Should().BeFalse();
        body.ErrorMessage.Should().Be("Invalid token");
        (await _todoistStore.ReadAsync()).Should().Be("todoist-persisted");
    }

    [Fact]
    public async Task ClearTodoistToken_RemovesAnyPersistedValue()
    {
        await _todoistStore.WriteAsync("todoist-to-go");

        var action = await _sut.ClearTodoistToken();

        GetBody<TodoistStatusResponse>(action).Configured.Should().BeFalse();
        (await _todoistStore.ReadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetTodoistProjectHistory_Returns200WithServiceResult()
    {
        var expected = new TodoistProjectHistoryResponse
        {
            NamesResolved = true,
            Projects =
            [
                new TodoistProjectHistoryEntry { DisplayName = "Inbox (default)", IsInbox = true, ProjectId = null },
                new TodoistProjectHistoryEntry { DisplayName = "Groceries", IsInbox = false, ProjectId = "2331547980" }
            ]
        };
        _historyServiceMock
            .Setup(s => s.GetProjectHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var action = await _sut.GetTodoistProjectHistory();

        var body = GetBody<TodoistProjectHistoryResponse>(action);
        body.Should().Be(expected);
        body.NamesResolved.Should().BeTrue();
        body.Projects.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTodoistProjectHistory_WhenTodoistUnreachable_Returns200WithDegradedResult()
    {
        // The endpoint must never return 500 when Todoist is unreachable —
        // name resolution is best-effort and degradation is surfaced via NamesResolved=false.
        var degraded = new TodoistProjectHistoryResponse
        {
            NameResolutionError = "Network error contacting Todoist: timeout",
            NamesResolved = false,
            Projects =
            [
                new TodoistProjectHistoryEntry { DisplayName = "Inbox (default)", IsInbox = true, ProjectId = null },
                new TodoistProjectHistoryEntry { DisplayName = null, IsInbox = false, ProjectId = "2331547980" }
            ]
        };
        _historyServiceMock
            .Setup(s => s.GetProjectHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(degraded);

        var action = await _sut.GetTodoistProjectHistory();

        var body = GetBody<TodoistProjectHistoryResponse>(action);
        body.NamesResolved.Should().BeFalse();
        body.NameResolutionError.Should().NotBeNullOrWhiteSpace();
        body.Projects[1].DisplayName.Should().BeNull();
    }

    private static T GetBody<T>(ActionResult<T> action) where T : class
    {
        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<T>().Subject;
    }

    /// <summary>In-memory <see cref="IClaudeModelStore"/> for controller-level tests.</summary>
    private sealed class FakeClaudeModelStore : IClaudeModelStore
    {
        private ClaudeModel _model = ClaudeModelCatalog.Default;

        public Task<ClaudeModel> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_model);

        public Task WriteAsync(ClaudeModel model, CancellationToken cancellationToken = default)
        {
            _model = model;
            return Task.CompletedTask;
        }
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

    /// <summary>In-memory <see cref="ITodoistTokenStore"/> for controller-level tests.</summary>
    private sealed class FakeTodoistTokenStore : ITodoistTokenStore
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

    /// <summary>
    /// Thin resolver that reads the supplied store only — user-secret fallback
    /// behaviour is exercised directly in <see cref="TodoistTokenResolverTests"/>.
    /// </summary>
    private sealed class ResolverOverStore(ITodoistTokenStore store) : ITodoistTokenResolver
    {
        public async Task<bool> HasTokenAsync(CancellationToken cancellationToken = default) =>
            !string.IsNullOrWhiteSpace(await store.ReadAsync(cancellationToken));

        public Task<string?> ResolveAsync(CancellationToken cancellationToken = default) =>
            store.ReadAsync(cancellationToken);
    }
}
