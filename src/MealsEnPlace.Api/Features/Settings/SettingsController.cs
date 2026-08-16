using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Settings endpoints covering the BYO Anthropic API key flow (MEP-032) and
/// the BYO Todoist API token flow (MEP-035). Every response shape carries at
/// most a boolean <c>Configured</c> indicator — the raw token is never returned
/// from any endpoint and is not written to logs. A failed Test Connection
/// call never overwrites a previously-valid stored token.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Produces("application/json")]
public class SettingsController(
    IAnthropicTestClient anthropicTestClient,
    IClaudeTokenStore claudeTokenStore,
    ITodoistTestClient todoistTestClient,
    ITodoistTokenResolver todoistTokenResolver,
    ITodoistTokenStore todoistTokenStore) : ControllerBase
{
    /// <summary>
    /// Deletes the persisted Anthropic API key. Subsequent Claude-backed
    /// operations take their deterministic-only branch until a new key is saved.
    /// </summary>
    [HttpDelete("claude/token")]
    [ProducesResponseType(typeof(ClaudeTokenStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaudeTokenStatusResponse>> ClearClaudeToken(CancellationToken cancellationToken = default)
    {
        await claudeTokenStore.ClearAsync(cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = false });
    }

    /// <summary>
    /// Deletes the persisted Todoist API token. The legacy user-secret fallback
    /// remains in effect if present.
    /// </summary>
    [HttpDelete("todoist/token")]
    [ProducesResponseType(typeof(TodoistStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TodoistStatusResponse>> ClearTodoistToken(CancellationToken cancellationToken = default)
    {
        await todoistTokenStore.ClearAsync(cancellationToken);
        var configured = await todoistTokenResolver.HasTokenAsync(cancellationToken);
        return Ok(new TodoistStatusResponse { Configured = configured });
    }

    /// <summary>Returns whether an Anthropic API key is currently configured.</summary>
    [HttpGet("claude/status")]
    [ProducesResponseType(typeof(ClaudeTokenStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaudeTokenStatusResponse>> GetClaudeStatus(CancellationToken cancellationToken = default)
    {
        var configured = await claudeTokenStore.HasTokenAsync(cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = configured });
    }

    /// <summary>
    /// Returns whether the Todoist integration has a token available from either
    /// the encrypted store or the legacy user-secret fallback.
    /// </summary>
    [HttpGet("todoist/status")]
    [ProducesResponseType(typeof(TodoistStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TodoistStatusResponse>> GetTodoistStatus(CancellationToken cancellationToken = default)
    {
        var configured = await todoistTokenResolver.HasTokenAsync(cancellationToken);
        return Ok(new TodoistStatusResponse { Configured = configured });
    }

    /// <summary>
    /// Persists the Anthropic API key to the encrypted local store. Returns only
    /// <c>Configured = true</c> on success — the raw key is never included in the
    /// response body.
    /// </summary>
    [HttpPost("claude/token")]
    [ProducesResponseType(typeof(ClaudeTokenStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClaudeTokenStatusResponse>> SaveClaudeToken(
        [FromBody] SaveClaudeTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = "Token is required."
            });
        }

        await claudeTokenStore.WriteAsync(request.Token, cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = true });
    }

    /// <summary>
    /// Persists the Todoist API token to the encrypted local store. Returns only
    /// <c>Configured = true</c> on success — the raw token is never included in the
    /// response body.
    /// </summary>
    [HttpPost("todoist/token")]
    [ProducesResponseType(typeof(TodoistStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoistStatusResponse>> SaveTodoistToken(
        [FromBody] SaveTodoistTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = "Token is required."
            });
        }

        await todoistTokenStore.WriteAsync(request.Token, cancellationToken);
        return Ok(new TodoistStatusResponse { Configured = true });
    }

    /// <summary>
    /// Issues a live Anthropic Messages API call using either the supplied
    /// candidate token or the currently persisted token. An invalid candidate
    /// never overwrites an already-valid stored key.
    /// </summary>
    [HttpPost("claude/test")]
    [ProducesResponseType(typeof(ClaudeTokenTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClaudeTokenTestResponse>> TestClaudeToken(
        [FromBody] TestClaudeTokenRequest? request,
        CancellationToken cancellationToken = default)
    {
        var candidate = request?.Token;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = await claudeTokenStore.ReadAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = "No token was supplied and no token is currently configured."
            });
        }

        var result = await anthropicTestClient.PingAsync(candidate, cancellationToken);
        return Ok(new ClaudeTokenTestResponse
        {
            ErrorMessage = result.ErrorMessage,
            Success = result.Success
        });
    }

    /// <summary>
    /// Issues a live Todoist <c>GET /rest/v2/projects</c> call using either the
    /// supplied candidate token or the currently resolved token (encrypted
    /// store first, user-secret fallback). An invalid candidate never overwrites
    /// an already-valid stored token.
    /// </summary>
    [HttpPost("todoist/test")]
    [ProducesResponseType(typeof(TodoistTokenTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoistTokenTestResponse>> TestTodoistToken(
        [FromBody] TestTodoistTokenRequest? request,
        CancellationToken cancellationToken = default)
    {
        var candidate = request?.Token;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = await todoistTokenResolver.ResolveAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = "No token was supplied and no token is currently configured."
            });
        }

        var result = await todoistTestClient.PingAsync(candidate, cancellationToken);
        return Ok(new TodoistTokenTestResponse
        {
            ErrorMessage = result.ErrorMessage,
            Success = result.Success
        });
    }
}
