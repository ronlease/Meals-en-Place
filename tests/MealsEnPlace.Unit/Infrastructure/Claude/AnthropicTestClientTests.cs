// Feature: Anthropic Test Connection client (MEP-032 / MEP-052)
//
// Scenario: PingAsync sends the currently selected model preference
// Scenario: PingAsync falls back to the catalog default when no preference is stored
// Scenario: Non-success response surfaces the Anthropic error message
// Scenario: Whitespace token throws ArgumentException before hitting the wire

using System.Net;
using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using Moq;
using Moq.Protected;

namespace MealsEnPlace.Unit.Infrastructure.Claude;

public sealed class AnthropicTestClientTests
{
    [Fact]
    public async Task PingAsync_SendsTheSelectedModelPreference()
    {
        // The request (and its content) is disposed inside PingAsync, so the body
        // must be read from within the handler rather than after the call returns.
        string? capturedBody = null;
        var handler = BuildHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sut = BuildClient(handler, new FakeClaudeModelStore(ClaudeModel.Opus5));

        var result = await sut.PingAsync("sk-ant-good");

        result.Success.Should().BeTrue();
        capturedBody.Should().Contain("\"claude-opus-5\"");
    }

    [Fact]
    public async Task PingAsync_WithNoStoredPreference_UsesCatalogDefault()
    {
        string? capturedBody = null;
        var handler = BuildHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sut = BuildClient(handler, new FakeClaudeModelStore(ClaudeModelCatalog.Default));

        await sut.PingAsync("sk-ant-good");

        capturedBody.Should().Contain($"\"{ClaudeModelCatalog.AnthropicModelId(ClaudeModelCatalog.Default)}\"");
    }

    [Fact]
    public async Task PingAsync_UnauthorizedResponse_SurfacesErrorMessage()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid x-api-key\"}}")
        }));
        var sut = BuildClient(handler, new FakeClaudeModelStore(ClaudeModelCatalog.Default));

        var result = await sut.PingAsync("sk-ant-bad");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("invalid x-api-key");
    }

    [Fact]
    public async Task PingAsync_WhitespaceToken_Throws()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sut = BuildClient(handler, new FakeClaudeModelStore(ClaudeModelCatalog.Default));

        var act = async () => await sut.PingAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static AnthropicTestClient BuildClient(HttpMessageHandler handler, IClaudeModelStore claudeModelStore)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("Anthropic"))
            .Returns(() => new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.anthropic.com")
            });
        return new AnthropicTestClient(claudeModelStore, factoryMock.Object);
    }

    private static HttpMessageHandler BuildHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) => respond(req, ct));
        return mock.Object;
    }

    /// <summary>Fixed-value <see cref="IClaudeModelStore"/> for client-level tests.</summary>
    private sealed class FakeClaudeModelStore(ClaudeModel model) : IClaudeModelStore
    {
        public Task<ClaudeModel> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(model);

        public Task WriteAsync(ClaudeModel model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
