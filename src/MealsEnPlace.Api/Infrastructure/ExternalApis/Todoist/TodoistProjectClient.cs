using System.Net.Http.Headers;
using System.Text.Json;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <inheritdoc cref="ITodoistProjectClient"/>
public sealed class TodoistProjectClient(IHttpClientFactory httpClientFactory) : ITodoistProjectClient
{
    private const string HttpClientName = "Todoist";
    private const int MaximumPages = 50;

    private static string ExtractErrorMessage(string body, string statusFallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Todoist returned HTTP {statusFallback}.";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() ?? $"Todoist returned HTTP {statusFallback}.";
            }
        }
        catch (JsonException)
        {
            // fall through to the raw body
        }

        return body.Length > 500 ? body[..500] : body;
    }

    public async Task<TodoistProjectListResult> GetProjectsAsync(
        string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TodoistProjectListResult
            {
                ErrorMessage = "No Todoist token is configured.",
                Succeeded = false
            };
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var accumulated = new List<TodoistProject>();
        string? cursor = null;

        for (var page = 0; page < MaximumPages; page++)
        {
            var path = cursor is null
                ? "/api/v1/projects"
                : $"/api/v1/projects?cursor={Uri.EscapeDataString(cursor)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new TodoistProjectListResult
                    {
                        ErrorMessage = ExtractErrorMessage(errorBody, response.StatusCode.ToString()),
                        Succeeded = false
                    };
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var (pageProjects, nextCursor) = ParsePageEnvelope(body);
                accumulated.AddRange(pageProjects);

                // Stop when there is no next cursor or the returned cursor equals the one
                // used for this request (guards against a malformed infinite-loop response).
                if (nextCursor is null || nextCursor == cursor)
                {
                    break;
                }

                cursor = nextCursor;
            }
            catch (HttpRequestException ex)
            {
                return new TodoistProjectListResult
                {
                    ErrorMessage = $"Network error contacting Todoist: {ex.Message}",
                    Succeeded = false
                };
            }
            catch (TaskCanceledException)
            {
                return new TodoistProjectListResult
                {
                    ErrorMessage = "Request to Todoist timed out.",
                    Succeeded = false
                };
            }
        }

        return new TodoistProjectListResult
        {
            Projects = accumulated,
            Succeeded = true
        };
    }

    private static (List<TodoistProject> Projects, string? NextCursor) ParsePageEnvelope(string body)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var envelope = JsonSerializer.Deserialize<TodoistProjectPageEnvelope>(body, options);
            if (envelope?.Results is null)
            {
                return ([], null);
            }

            var projects = envelope.Results
                .Where(e => !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name))
                .Select(e => new TodoistProject { Id = e.Id!, Name = e.Name! })
                .ToList();

            return (projects, envelope.NextCursor);
        }
        catch (JsonException)
        {
            return ([], null);
        }
    }
}
