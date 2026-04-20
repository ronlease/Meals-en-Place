using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <inheritdoc cref="ITodoistClient"/>
public sealed class TodoistClient(
    IHttpClientFactory httpClientFactory,
    IOptions<TodoistOptions> options) : ITodoistClient
{
    private const string HttpClientName = "Todoist";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task CloseTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/rest/v2/tasks/{taskId}/close");
        await SendAsync(request, cancellationToken);
    }

    public async Task<string> CreateTaskAsync(
        TodoistTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var request = BuildRequest(HttpMethod.Post, "/rest/v2/tasks", payload);
        using var response = await SendAsync(request, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<TodoistTaskEnvelope>(SerializerOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(body?.Id))
        {
            throw new InvalidOperationException("Todoist response did not include a task id.");
        }

        return body.Id;
    }

    public async Task UpdateTaskAsync(
        string taskId,
        TodoistTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(payload);

        using var request = BuildRequest(HttpMethod.Post, $"/rest/v2/tasks/{taskId}", payload);
        await SendAsync(request, cancellationToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, TodoistTaskPayload payload)
    {
        return new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = options.Value.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Todoist integration is not configured. Set the Todoist:Token user secret.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new TodoistApiException(
                (int)response.StatusCode,
                ExtractErrorMessage(body, response.StatusCode.ToString()));
        }

        return response;
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

    private sealed class TodoistTaskEnvelope
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }
}

/// <summary>Wraps a non-success response from the Todoist API for the push-target caller.</summary>
public sealed class TodoistApiException(int statusCode, string message) : Exception(message)
{
    /// <summary>HTTP status code returned by Todoist.</summary>
    public int StatusCode { get; } = statusCode;
}
