using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Tasks;
using TaskManager.Application.Users;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Security;
using Xunit.Abstractions;

namespace TaskManager.Api.Tests.Tasks;

public sealed class TaskEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _anonymous;
    private HttpClient _ada = null!;
    private HttpClient _grace = null!;
    private string _adaToken = null!;

    public TaskEndpointTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _anonymous = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        _adaToken = await SignUp("ada@example.com", "Ada Lovelace");
        _ada = Authorised(_adaToken);
        _grace = Authorised(await SignUp("grace@example.com", "Grace Hopper"));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetTasks_WithoutToken_ReturnsUnauthorized()
    {
        (await _anonymous.GetAsync("/api/tasks")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTasks_WithExpiredToken_ReturnsUnauthorized()
    {
        var expired = Authorised(ExpiredTokenFor("ada@example.com", "Ada Lovelace"));

        (await expired.GetAsync("/api/tasks")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EveryWriteRoute_WithoutToken_ReturnsUnauthorized(string method)
    {
        // POST addresses the collection; PUT and DELETE address one task.
        var url = method == "POST" ? "/api/tasks" : $"/api/tasks/{Guid.NewGuid()}";

        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new CreateTaskRequest("Write the report", null, null), options: Json),
        };

        (await _anonymous.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTask_WhenOwned_ReturnsTask()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", "Q1 batch", Today.AddDays(30)));

        var response = await _ada.GetAsync($"/api/tasks/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var task = await Read(response);
        task.Id.ShouldBe(created.Id);
        task.Title.ShouldBe("Write the report");
        task.Description.ShouldBe("Q1 batch");
        task.Status.ShouldBe(TaskItemStatus.Pending);
        task.DueDate.ShouldBe(Today.AddDays(30));
        task.CreatedAt.ShouldNotBe(default);
        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetTask_WhenMissing_ReturnsNotFound()
    {
        var response = await _ada.GetAsync($"/api/tasks/{Guid.Empty}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problem(response)).Detail.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task CreateTask_ReturnsCreatedWithALocationHeaderThatServesTheTask()
    {
        var response = await _ada.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest("Prepare invoices", "Q1 batch", Today.AddDays(60)),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await Read(response);
        var location = response.Headers.Location.ShouldNotBeNull();

        location.ToString().ShouldBe($"/api/tasks/{created.Id}");

        var followed = await _ada.GetAsync(location);

        followed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read(followed)).Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task CreateTask_WithOnlyATitle_StartsAsPendingWithoutADueDate()
    {
        var created = await Create(_ada, new CreateTaskRequest("Book the meeting room", null, null));

        created.Status.ShouldBe(TaskItemStatus.Pending);
        created.DueDate.ShouldBeNull();
        created.Description.ShouldBeNull();
        created.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTask_WithoutATitle_ReturnsAValidationProblem(string title)
    {
        var response = await _ada.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(title, null, null), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await ValidationErrors(response)).Keys.ShouldContain("title");
    }

    [Fact]
    public async Task CreateTask_WithATitleLongerThanTheLimit_ReturnsAValidationProblem()
    {
        var response = await _ada.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest(new string('a', 201), null, null),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ValidationErrors(response)).Keys.ShouldContain("title");
    }

    [Fact]
    public async Task CreateTask_WithADescriptionLongerThanTheLimit_ReturnsAValidationProblem()
    {
        var response = await _ada.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest("Write the report", new string('a', 2001), null),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ValidationErrors(response)).Keys.ShouldContain("description");
    }

    [Fact]
    public async Task CreateTask_WithAPastDueDate_ReturnsBadRequestWithTheDomainMessage()
    {
        var response = await _ada.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest("Late task", null, new DateOnly(2020, 1, 1)),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await Problem(response)).Detail.ShouldBe("Due date cannot be in the past.");
    }

    [Fact]
    public async Task UpdateTask_WhenOwned_ReturnsTheUpdatedTask()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", "Q1 batch", Today.AddDays(30)));

        var response = await _ada.PutAsJsonAsync(
            $"/api/tasks/{created.Id}",
            new UpdateTaskRequest("Write the annual report", "With the Q4 figures", TaskItemStatus.InProgress, Today.AddDays(45)),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await Read(response);
        updated.Title.ShouldBe("Write the annual report");
        updated.Description.ShouldBe("With the Q4 figures");
        updated.Status.ShouldBe(TaskItemStatus.InProgress);
        updated.DueDate.ShouldBe(Today.AddDays(45));

        (await Read(await _ada.GetAsync($"/api/tasks/{created.Id}"))).Title.ShouldBe("Write the annual report");
    }

    [Fact]
    public async Task UpdateTask_ToDone_RecordsTheCompletionTime()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, null));

        var updated = await Update(_ada, created.Id, Request(status: TaskItemStatus.Done));

        updated.Status.ShouldBe(TaskItemStatus.Done);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateTask_OutOfDone_ClearsTheCompletionTime()
    {
        var created = await Create(_ada, new CreateTaskRequest("Archive last sprint", null, null));
        await Update(_ada, created.Id, Request(status: TaskItemStatus.Done));

        var reopened = await Update(_ada, created.Id, Request(status: TaskItemStatus.Pending));

        reopened.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateTask_WithoutADueDate_ClearsIt()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, Today.AddDays(30)));

        var updated = await Update(_ada, created.Id, Request(dueDate: null));

        updated.DueDate.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateTask_WhenMissing_ReturnsNotFound()
    {
        var response = await _ada.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}", Request(), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Asserted through the body, not just the status: an unmatched route answers 404 as well,
        // and only the service's answer carries a problem document.
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await Problem(response)).Detail.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task UpdateTask_WithoutATitle_ReturnsAValidationProblem()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, null));

        var response = await _ada.PutAsJsonAsync($"/api/tasks/{created.Id}", Request(title: "  "), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ValidationErrors(response)).Keys.ShouldContain("title");
    }

    [Fact]
    public async Task DeleteTask_WhenOwned_ReturnsNoContentAndTheTaskIsGone()
    {
        var kept = await Create(_ada, new CreateTaskRequest("Write the report", null, null));
        var doomed = await Create(_ada, new CreateTaskRequest("Review the deck", null, null));

        var response = await _ada.DeleteAsync($"/api/tasks/{doomed.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();

        (await _ada.GetAsync($"/api/tasks/{doomed.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var remaining = await List(_ada);
        remaining.Count.ShouldBe(1);
        remaining[0].Id.ShouldBe(kept.Id);
    }

    [Fact]
    public async Task DeleteTask_WhenMissing_ReturnsNotFound()
    {
        var response = await _ada.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await Problem(response)).Detail.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task GetTasks_ReturnsOnlyTheCallersTasks()
    {
        await Create(_ada, new CreateTaskRequest("Write the report", null, null));
        await Create(_ada, new CreateTaskRequest("Review the deck", null, null));
        await Create(_grace, new CreateTaskRequest("Quarterly forecast", null, null));

        var mine = await List(_ada);

        mine.Count.ShouldBe(2);
        mine.Select(task => task.Title).ShouldNotContain("Quarterly forecast");
    }

    [Fact]
    public async Task GetTasks_OrdersByDueDateWithUndatedTasksLast()
    {
        await Create(_ada, new CreateTaskRequest("Write the report", null, Today.AddDays(10)));
        await Create(_ada, new CreateTaskRequest("Review the deck", null, Today.AddDays(5)));
        await Create(_ada, new CreateTaskRequest("Archive last sprint", null, null));

        var mine = await List(_ada);

        mine.Select(task => task.Title)
            .ShouldBe(["Review the deck", "Write the report", "Archive last sprint"]);
    }

    [Fact]
    public async Task GetTasks_FilteredByStatus_ReturnsOnlyThatStatus()
    {
        await Create(_ada, new CreateTaskRequest("Write the report", null, null));
        var deck = await Create(_ada, new CreateTaskRequest("Review the deck", null, null));
        await Update(_ada, deck.Id, Request(title: "Review the deck", status: TaskItemStatus.InProgress));

        var inProgress = await List(_ada, "?status=InProgress");

        inProgress.Count.ShouldBe(1);
        inProgress[0].Title.ShouldBe("Review the deck");
    }

    [Theory]
    [InlineData("report")]
    [InlineData("REPORT")]
    public async Task GetTasks_SearchedByTitle_ReturnsTheMatches(string search)
    {
        await Create(_ada, new CreateTaskRequest("Write the report", null, null));
        await Create(_ada, new CreateTaskRequest("Review the deck", null, null));

        var found = await List(_ada, $"?search={search}");

        found.Count.ShouldBe(1);
        found[0].Title.ShouldBe("Write the report");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("inprogress")]
    public async Task GetTasks_WhenBindingThrowsOnAnUnparseableStatus_StillReturnsABadRequestProblem(string status)
    {
        var response = await DevelopmentStyleBinding().GetAsync($"/api/tasks?status={status}");

        _output.WriteLine($"status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"body: <{await response.Content.ReadAsStringAsync()}>");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await Problem(response)).Status.ShouldBe(400);
    }

    [Fact]
    public async Task GetTasks_WithAStatusOutsideTheEnumeration_ReturnsAValidationProblem()
    {
        var deck = await Create(_ada, new CreateTaskRequest("Review the deck", null, null));
        await Update(_ada, deck.Id, Request(title: "Review the deck", status: TaskItemStatus.InProgress));

        var response = await _ada.GetAsync("/api/tasks?status=99");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await ValidationErrors(response)).Keys.ShouldContain("status");
    }

    [Fact]
    public async Task GetTasks_AcceptsTheNumericOrdinalOfAKnownStatus()
    {
        var deck = await Create(_ada, new CreateTaskRequest("Review the deck", null, null));
        await Update(_ada, deck.Id, Request(title: "Review the deck", status: TaskItemStatus.InProgress));
        await Create(_ada, new CreateTaskRequest("Write the report", null, null));

        var inProgress = await List(_ada, "?status=1");

        inProgress.Count.ShouldBe(1);
        inProgress[0].Title.ShouldBe("Review the deck");
    }

    [Fact]
    public async Task GetTasks_WithAnUnparseableStatusFilter_ReturnsBadRequest()
    {
        var response = await _ada.GetAsync("/api/tasks?status=NotAStatus");

        _output.WriteLine($"status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"content-type: {response.Content.Headers.ContentType?.ToString() ?? "(none)"}");
        _output.WriteLine($"body: <{await response.Content.ReadAsStringAsync()}>");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTask_WhenOwnedByAnotherUser_ReturnsNotFound()
    {
        var hers = await Create(_grace, new CreateTaskRequest("Quarterly forecast", null, null));

        var response = await _ada.GetAsync($"/api/tasks/{hers.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problem(response)).Detail.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task GetTask_ForAForeignTask_IsIndistinguishableFromOneThatDoesNotExist()
    {
        var hers = await Create(_grace, new CreateTaskRequest("Quarterly forecast", null, null));

        var foreign = await _ada.GetAsync($"/api/tasks/{hers.Id}");
        var missing = await _ada.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        foreign.StatusCode.ShouldBe(missing.StatusCode);
        (await Problem(foreign)).Detail.ShouldBe((await Problem(missing)).Detail);
    }

    [Fact]
    public async Task UpdateTask_WhenOwnedByAnotherUser_ReturnsNotFoundAndLeavesItUnchanged()
    {
        var hers = await Create(_grace, new CreateTaskRequest("Quarterly forecast", null, null));

        var response = await _ada.PutAsJsonAsync($"/api/tasks/{hers.Id}", Request(title: "Hijacked"), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Read(await _grace.GetAsync($"/api/tasks/{hers.Id}"))).Title.ShouldBe("Quarterly forecast");
    }

    [Fact]
    public async Task DeleteTask_WhenOwnedByAnotherUser_ReturnsNotFoundAndTheTaskSurvives()
    {
        var hers = await Create(_grace, new CreateTaskRequest("Quarterly forecast", null, null));

        var response = await _ada.DeleteAsync($"/api/tasks/{hers.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _grace.GetAsync($"/api/tasks/{hers.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Status_CrossesTheWireAsAStringRatherThanANumber()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, null));

        var payload = await (await _ada.GetAsync($"/api/tasks/{created.Id}")).Content.ReadAsStringAsync();

        payload.ShouldContain("\"status\":\"Pending\"");
        payload.ShouldNotContain("\"status\":0");
    }

    [Fact]
    public async Task DueDate_CrossesTheWireAsAPlainDate()
    {
        var due = Today.AddDays(30);
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, due));

        var payload = await (await _ada.GetAsync($"/api/tasks/{created.Id}")).Content.ReadAsStringAsync();

        payload.ShouldContain($"\"dueDate\":\"{due:yyyy-MM-dd}\"");
    }

    [Fact]
    public async Task Task_NeverExposesTheOwner()
    {
        var created = await Create(_ada, new CreateTaskRequest("Write the report", null, null));

        var payload = await (await _ada.GetAsync($"/api/tasks/{created.Id}")).Content.ReadAsStringAsync();

        payload.ShouldNotContain("ownerId", Case.Insensitive);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static UpdateTaskRequest Request(
        string title = "Write the report",
        string? description = null,
        TaskItemStatus status = TaskItemStatus.Pending,
        DateOnly? dueDate = null) =>
        new(title, description, status, dueDate);

    private static async Task<TaskDto> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TaskDto>(Json))!;

    private static async Task<ProblemResponse> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;

    private static async Task<Dictionary<string, string[]>> ValidationErrors(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ValidationProblemResponse>())!.Errors;

    private static async Task<TaskDto> Create(HttpClient client, CreateTaskRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", request, Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await Read(response);
    }

    private static async Task<TaskDto> Update(HttpClient client, Guid id, UpdateTaskRequest request)
    {
        var response = await client.PutAsJsonAsync($"/api/tasks/{id}", request, Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await Read(response);
    }

    private static async Task<IReadOnlyList<TaskDto>> List(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/tasks{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<TaskDto>>(Json))!;
    }

    private async Task<string> SignUp(string email, string displayName)
    {
        var response = await _anonymous.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, displayName, "Passw0rd!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<AuthResult>();

        return result!.AccessToken;
    }

    /// <summary>
    /// A client whose server binds parameters the way Development does.
    /// <c>RouteHandlerOptions.ThrowOnBadRequest</c> defaults to true only in that environment, so
    /// the test host, which runs as "Testing", would otherwise never exercise the throwing path.
    /// </summary>
    private HttpClient DevelopmentStyleBinding()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true)))
            .CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adaToken);

        return client;
    }

    private HttpClient Authorised(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private string ExpiredTokenFor(string email, string displayName)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow.AddHours(-2));

        var user = User.Register(email, displayName, "hash", DateTimeOffset.UtcNow);

        return new JwtTokenGenerator(Options.Create(_factory.Jwt), clock).Generate(user).Value;
    }

    private sealed record ProblemResponse(string Title, string Detail, int Status);

    private sealed record ValidationProblemResponse(string Title, int Status, Dictionary<string, string[]> Errors);
}
