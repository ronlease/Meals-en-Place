using System.Net.Http.Headers;
using System.Text.Json;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <inheritdoc cref="ITodoistTestClient"/>
public sealed class TodoistTestClient(IHttpClientFactory httpClientFactory) : ITodoistTestClient
{
    private const string HttpClientName = "Todoist";

    public async Task<TodoistTestResult> PingAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rest/v2/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new TodoistTestResult { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TodoistTestResult
            {
                ErrorMessage = ExtractErrorMessage(body, response.StatusCode.ToString()),
                Success = false
            };
        }
        catch (HttpRequestException ex)
        {
            return new TodoistTestResult
            {
                ErrorMessage = $"Network error contacting Todoist: {ex.Message}",
                Success = false
            };
        }
        catch (TaskCanceledException)
        {
            return new TodoistTestResult
            {
                ErrorMessage = "Request to Todoist timed out.",
                Success = false
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
}
