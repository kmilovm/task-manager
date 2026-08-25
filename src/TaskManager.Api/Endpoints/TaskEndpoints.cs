using System.Security.Claims;
using TaskManager.Api.Common;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;

namespace TaskManager.Api.Endpoints;

internal static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal principal,
                ITaskService tasks,
                TaskItemStatus? status,
                string? search,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await tasks.GetAllAsync(principal.GetUserId(), status, search, cancellationToken)))
            .WithSummary("Lists the signed-in account's tasks, optionally filtered by status and title.")
            .Produces<IReadOnlyList<TaskDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal principal,
                ITaskService tasks,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await tasks.GetByIdAsync(principal.GetUserId(), id, cancellationToken)))
            .WithSummary("Returns one task owned by the signed-in account.")
            .Produces<TaskDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateTaskRequest request,
                ClaimsPrincipal principal,
                ITaskService tasks,
                CancellationToken cancellationToken) =>
            {
                var created = await tasks.CreateAsync(principal.GetUserId(), request, cancellationToken);

                return TypedResults.Created($"/api/tasks/{created.Id}", created);
            })
            .WithSummary("Creates a task owned by the signed-in account.")
            .Produces<TaskDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateTaskRequest request,
                ClaimsPrincipal principal,
                ITaskService tasks,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await tasks.UpdateAsync(principal.GetUserId(), id, request, cancellationToken)))
            .WithSummary("Replaces a task owned by the signed-in account. Omitted optional fields are cleared.")
            .Produces<TaskDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal principal,
                ITaskService tasks,
                CancellationToken cancellationToken) =>
            {
                await tasks.DeleteAsync(principal.GetUserId(), id, cancellationToken);

                return TypedResults.NoContent();
            })
            .WithSummary("Deletes a task owned by the signed-in account, permanently.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
