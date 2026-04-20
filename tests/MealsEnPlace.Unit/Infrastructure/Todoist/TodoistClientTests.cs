// Feature: Todoist REST API client (MEP-028 / MEP-029 infrastructure)
//
// Scenario: CreateTaskAsync posts to /rest/v2/tasks with Bearer auth and returns the new task id
// Scenario: CreateTaskAsync propagates a TodoistApiException on non-success status
// Scenario: UpdateTaskAsync posts to /rest/v2/tasks/{id} with the updated payload
// Scenario: CloseTaskAsync posts to /rest/v2/tasks/{id}/close
// Scenario: Any call without a configured token throws InvalidOperationException before hitting the wire

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace MealsEnPlace.Unit.Infrastructure.Todoist;

public sealed class TodoistClientTests
{
    [Fact]
    public async Task CreateTaskAsync_PostsToTasks_WithBearerAuth_AndReturnsTaskId()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = BuildHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "remote-123" })
            };
        });
        var sut = BuildClient(handler, token: "test-token");

        // Act
        var id = await sut.CreateTaskAsync(new TodoistTaskPayload
        {
            Content = "Buy milk",
            DueDate = "2026-05-01",
            ProjectId = "project-abc"
        });

        // Assert
        id.Should().Be("remote-123");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/rest/v2/tasks");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-token");
        capturedBody.Should().Contain("\"content\":\"Buy milk\"");
        capturedBody.Should().Contain("\"due_date\":\"2026-05-01\"");
        capturedBody.Should().Contain("\"project_id\":\"project-abc\"");
    }

    [Fact]
    public async Task CreateTaskAsync_NonSuccess_ThrowsTodoistApiExceptionWithStatusAndMessage()
    {
        // Arrange
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid token\"}")
        }));
        var sut = BuildClient(handler, token: "bad-token");

        // Act
        var act = async () => await sut.CreateTaskAsync(new TodoistTaskPayload { Content = "Buy milk" });

        // Assert
        var ex = await act.Should().ThrowAsync<TodoistApiException>();
        ex.Which.StatusCode.Should().Be(401);
        ex.Which.Message.Should().Contain("Invalid token");
    }

    [Fact]
    public async Task UpdateTaskAsync_PostsToTaskUrl_WithPayload()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = BuildHandler((req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sut = BuildClient(handler, token: "test-token");

        // Act
        await sut.UpdateTaskAsync("remote-456", new TodoistTaskPayload { Content = "Updated content" });

        // Assert
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/rest/v2/tasks/remote-456");
    }

    [Fact]
    public async Task CloseTaskAsync_PostsToCloseUrl()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = BuildHandler((req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var sut = BuildClient(handler, token: "test-token");

        // Act
        await sut.CloseTaskAsync("remote-789");

        // Assert
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/rest/v2/tasks/remote-789/close");
    }

    [Fact]
    public async Task CreateTaskAsync_WithoutToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = BuildHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sut = BuildClient(handler, token: null);

        // Act
        var act = async () => await sut.CreateTaskAsync(new TodoistTaskPayload { Content = "whatever" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    private static TodoistClient BuildClient(HttpMessageHandler handler, string? token)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("Todoist"))
            .Returns(() => new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.todoist.com")
            });

        var options = Options.Create(new TodoistOptions { Token = token });
        return new TodoistClient(factoryMock.Object, options);
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
