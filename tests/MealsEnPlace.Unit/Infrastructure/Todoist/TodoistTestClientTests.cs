// Feature: Todoist Test Connection client (MEP-035 / MEP-042)
//
// Scenario: Successful GET /api/v1/projects returns Success=true
// Scenario: Non-success response surfaces the Todoist error message
// Scenario: Network error returns a friendly failure result
// Scenario: Bearer token is attached to the Authorization header
// Scenario: Whitespace token throws ArgumentException before hitting the wire

using System.Net;
using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Moq;
using Moq.Protected;

namespace MealsEnPlace.Unit.Infrastructure.Todoist;

public sealed class TodoistTestClientTests
{
    [Fact]
    public async Task PingAsync_Success_ReturnsSuccessTrueAndHitsProjectsEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = BuildHandler((req, _) =>
        {
            captured = req;
            // Realistic API v1 envelope — PingAsync checks status only, not body shape.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[],\"next_cursor\":null}")
            });
        });
        var sut = BuildClient(handler);

        var result = await sut.PingAsync("good-token");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsolutePath.Should().Be("/api/v1/projects");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("good-token");
    }

    [Fact]
    public async Task PingAsync_UnauthorizedResponse_SurfacesErrorMessage()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid token\"}")
        }));
        var sut = BuildClient(handler);

        var result = await sut.PingAsync("bad-token");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid token");
    }

    [Fact]
    public async Task PingAsync_NetworkError_ReturnsFriendlyFailure()
    {
        var handler = BuildHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = BuildClient(handler);

        var result = await sut.PingAsync("any-token");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error").And.Contain("Connection refused");
    }

    [Fact]
    public async Task PingAsync_WhitespaceToken_Throws()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sut = BuildClient(handler);

        var act = async () => await sut.PingAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static TodoistTestClient BuildClient(HttpMessageHandler handler)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("Todoist"))
            .Returns(() => new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.todoist.com")
            });
        return new TodoistTestClient(factoryMock.Object);
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
}
