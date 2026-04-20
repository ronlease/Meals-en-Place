using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Settings endpoints covering the BYO Anthropic API key flow and Todoist
/// integration status. Every response shape carries at most a boolean
/// <c>Configured</c> indicator — the raw token is never returned from any
/// endpoint and is not written to logs.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Produces("application/json")]
public class SettingsController(
    IAnthropicTestClient anthropicTestClient,
    IClaudeTokenStore tokenStore,
    IOptions<TodoistOptions> todoistOptions) : ControllerBase
{
    /// <summary>
    /// Deletes the persisted Anthropic API key. Subsequent Claude-backed
    /// operations take their deterministic-only branch until a new key is saved.
    /// </summary>
    [HttpDelete("claude/token")]
    [ProducesResponseType(typeof(ClaudeTokenStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaudeTokenStatusResponse>> ClearClaudeToken(CancellationToken cancellationToken = default)
    {
        await tokenStore.ClearAsync(cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = false });
    }

    /// <summary>Returns whether an Anthropic API key is currently configured.</summary>
    [HttpGet("claude/status")]
    [ProducesResponseType(typeof(ClaudeTokenStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaudeTokenStatusResponse>> GetClaudeStatus(CancellationToken cancellationToken = default)
    {
        var configured = await tokenStore.HasTokenAsync(cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = configured });
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

        await tokenStore.WriteAsync(request.Token, cancellationToken);
        return Ok(new ClaudeTokenStatusResponse { Configured = true });
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
            candidate = await tokenStore.ReadAsync(cancellationToken);
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
    /// Returns whether the Todoist integration has a token available. MEP-028
    /// reads the token from the <c>Todoist:Token</c> user secret; MEP-035 will
    /// later add a Settings-page flow and shift storage to DataProtection.
    /// </summary>
    [HttpGet("todoist/status")]
    [ProducesResponseType(typeof(TodoistStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<TodoistStatusResponse> GetTodoistStatus()
    {
        return Ok(new TodoistStatusResponse { Configured = todoistOptions.Value.IsConfigured });
    }
}
