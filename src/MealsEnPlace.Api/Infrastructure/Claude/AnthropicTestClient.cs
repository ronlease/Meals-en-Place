using System.Net.Http.Json;
using System.Text.Json;

namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// HTTP implementation of <see cref="IAnthropicTestClient"/>. Posts a single
/// low-cost Messages API request to <c>https://api.anthropic.com/v1/messages</c>
/// using the supplied token. Used only for the Test Connection endpoint.
/// </summary>
public sealed class AnthropicTestClient(IHttpClientFactory httpClientFactory) : IAnthropicTestClient
{
    private const string AnthropicVersion = "2023-06-01";
    private const string HttpClientName = "Anthropic";
    private const string Model = "claude-haiku-4-5";

    public async Task<AnthropicTestResult> PingAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(new
            {
                max_tokens = 1,
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                },
                model = Model
            })
        };
        request.Headers.Add("x-api-key", token);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new AnthropicTestResult { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new AnthropicTestResult
            {
                ErrorMessage = ExtractErrorMessage(body, response.StatusCode.ToString()),
                Success = false
            };
        }
        catch (HttpRequestException ex)
        {
            return new AnthropicTestResult
            {
                ErrorMessage = $"Network error contacting Anthropic: {ex.Message}",
                Success = false
            };
        }
        catch (TaskCanceledException)
        {
            return new AnthropicTestResult
            {
                ErrorMessage = "Request to Anthropic timed out.",
                Success = false
            };
        }
    }

    private static string ExtractErrorMessage(string body, string statusFallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Anthropic returned HTTP {statusFallback}.";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? $"Anthropic returned HTTP {statusFallback}.";
            }
        }
        catch (JsonException)
        {
            // fall through to the raw body
        }

        return body.Length > 500 ? body[..500] : body;
    }
}
