using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <inheritdoc cref="ITodoistProjectClient"/>
public sealed class TodoistProjectClient(IHttpClientFactory httpClientFactory) : ITodoistProjectClient
{
    private const string HttpClientName = "Todoist";

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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rest/v2/projects");
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
            var projects = ParseProjects(body);
            return new TodoistProjectListResult
            {
                Projects = projects,
                Succeeded = true
            };
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

    private static IReadOnlyList<TodoistProject> ParseProjects(string body)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var envelopes = JsonSerializer.Deserialize<List<TodoistProjectEnvelope>>(body, options);
            if (envelopes is null)
            {
                return [];
            }

            return envelopes
                .Where(e => !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name))
                .Select(e => new TodoistProject { Id = e.Id!, Name = e.Name! })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class TodoistProjectEnvelope
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
