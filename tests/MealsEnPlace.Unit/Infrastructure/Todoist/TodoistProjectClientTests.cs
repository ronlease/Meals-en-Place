// Feature: Todoist Project Client (MEP-036)
//
// Scenario: Successful GET /projects returns parsed project list
// Scenario: Non-success response surfaces the Todoist error message
// Scenario: Network error returns a friendly failure result
// Scenario: Timeout returns a friendly failure result
// Scenario: Bearer token is attached to the Authorization header
// Scenario: Whitespace token returns a failure result without making a network call
// Scenario: Malformed JSON body returns an empty list with Succeeded = true

using System.Net;
using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Moq;
using Moq.Protected;

namespace MealsEnPlace.Unit.Infrastructure.Todoist;

public sealed class TodoistProjectClientTests
{
    [Fact]
    public async Task GetProjectsAsync_Success_ReturnsParsedProjects()
    {
        var json = """
            [
              {"id":"111","name":"Groceries"},
              {"id":"222","name":"Meals"}
            ]
            """;
        HttpRequestMessage? captured = null;
        var handler = BuildHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        });
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("good-token");

        result.Succeeded.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Projects.Should().HaveCount(2);
        result.Projects[0].Id.Should().Be("111");
        result.Projects[0].Name.Should().Be("Groceries");
        result.Projects[1].Id.Should().Be("222");
        result.Projects[1].Name.Should().Be("Meals");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsolutePath.Should().Be("/rest/v2/projects");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("good-token");
    }

    [Fact]
    public async Task GetProjectsAsync_UnauthorizedResponse_SurfacesErrorMessage()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid token\"}")
        }));
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("bad-token");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid token");
        result.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProjectsAsync_NetworkError_ReturnsFriendlyFailure()
    {
        var handler = BuildHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("any-token");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error").And.Contain("Connection refused");
    }

    [Fact]
    public async Task GetProjectsAsync_Timeout_ReturnsFriendlyFailure()
    {
        var handler = BuildHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("any-token");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task GetProjectsAsync_WhitespaceToken_ReturnsFailureWithoutNetworkCall()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var sut = BuildClient(handlerMock.Object);

        var result = await sut.GetProjectsAsync("   ");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        // Strict mock would throw if SendAsync were called.
        handlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetProjectsAsync_MalformedJson_ReturnsSucceededWithEmptyList()
    {
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        }));
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("good-token");

        result.Succeeded.Should().BeTrue();
        result.Projects.Should().BeEmpty();
    }

    private static TodoistProjectClient BuildClient(HttpMessageHandler handler)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("Todoist"))
            .Returns(() => new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.todoist.com")
            });
        return new TodoistProjectClient(factoryMock.Object);
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
