// Feature: Todoist Project Client (MEP-036 / MEP-042)
//
// Scenario: Successful GET /projects returns parsed project list from paginated envelope
// Scenario: Non-success response surfaces the Todoist error message
// Scenario: Network error returns a friendly failure result
// Scenario: Timeout returns a friendly failure result
// Scenario: Bearer token is attached to the Authorization header
// Scenario: Whitespace token returns a failure result without making a network call
// Scenario: Malformed JSON body returns an empty list with Succeeded = true
// Scenario: Empty results array returns Succeeded = true with empty list
// Scenario: Multi-page cursor-following collects all projects across pages
// Scenario: Mid-pagination network error degrades to Succeeded = false

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
            {
              "results": [
                {"id":"111","name":"Groceries"},
                {"id":"222","name":"Meals"}
              ],
              "next_cursor": null
            }
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
        captured.RequestUri!.AbsolutePath.Should().Be("/api/v1/projects");
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

    [Fact]
    public async Task GetProjectsAsync_EmptyResults_ReturnsSucceededWithEmptyList()
    {
        var json = """{"results":[],"next_cursor":null}""";
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));
        var sut = BuildClient(handler);

        var result = await sut.GetProjectsAsync("good-token");

        result.Succeeded.Should().BeTrue();
        result.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProjectsAsync_MultiPage_FollowsCursorAndCollectsAllProjects()
    {
        // Arrange — page 1 returns cursor "page2", page 2 returns null cursor (last page).
        var page1Json = """
            {
              "results": [{"id":"111","name":"Groceries"}],
              "next_cursor": "page2"
            }
            """;
        var page2Json = """
            {
              "results": [{"id":"222","name":"Meals"}],
              "next_cursor": null
            }
            """;

        var requestCount = 0;
        string? secondRequestQuery = null;

        var handler = BuildHandler((req, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(page1Json)
                });
            }

            secondRequestQuery = req.RequestUri?.Query;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page2Json)
            });
        });
        var sut = BuildClient(handler);

        // Act
        var result = await sut.GetProjectsAsync("good-token");

        // Assert — both pages were fetched and merged
        result.Succeeded.Should().BeTrue();
        result.Projects.Should().HaveCount(2);
        result.Projects[0].Id.Should().Be("111");
        result.Projects[1].Id.Should().Be("222");
        requestCount.Should().Be(2);
        secondRequestQuery.Should().Contain("cursor=page2");
    }

    [Fact]
    public async Task GetProjectsAsync_MidPaginationNetworkError_DegradesFailed()
    {
        // Arrange — page 1 succeeds with a cursor, page 2 throws a network error.
        var page1Json = """{"results":[{"id":"111","name":"Groceries"}],"next_cursor":"page2"}""";

        var requestCount = 0;
        var handler = BuildHandler((_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(page1Json)
                });
            }

            return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection reset"));
        });
        var sut = BuildClient(handler);

        // Act
        var result = await sut.GetProjectsAsync("good-token");

        // Assert — degrades to Succeeded = false rather than returning a partial list silently
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error").And.Contain("Connection reset");
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
