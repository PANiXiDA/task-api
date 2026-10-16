using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Tasks;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasks(this IEndpointRouteBuilder endpoints)
    {
        var tasks = endpoints.MapGroup("/tasks");
        tasks.MapPost("/", CreateAsync).WithName("CreateTask");
        tasks.MapGet("/", GetAllAsync).WithName("GetTasks");
        tasks.MapPut("/{id}/complete", CompleteAsync).WithName("CompleteTask");
        tasks.MapDelete("/{id}", DeleteAsync).WithName("DeleteTask");
        return endpoints;
    }

    private static async Task<Results<Created<TaskResponse>, ValidationProblem>> CreateAsync(
        CreateTaskRequest request, TasksService service, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > TaskItem.MaxTitleLength)
        {
            errors["title"] = ["Title must contain non-whitespace characters and be at most 200 characters long."];
        }

        if (!Enum.IsDefined(request.Priority))
        {
            errors["priority"] = ["Priority must be Low, Medium or High."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var task = await service.CreateAsync(request.Title!, request.Priority, cancellationToken);
        return TypedResults.Created("/tasks", TaskResponse.From(task));
    }

    private static async Task<Ok<TaskResponse[]>> GetAllAsync(TasksService service, CancellationToken cancellationToken)
    {
        var tasks = await service.GetAllAsync(cancellationToken);
        return TypedResults.Ok(tasks.Select(TaskResponse.From).ToArray());
    }

    private static async Task<Results<Ok<TaskResponse>, NotFound, ProblemHttpResult>> CompleteAsync(
        Guid id, TasksService service, CancellationToken cancellationToken)
    {
        var result = await service.CompleteAsync(id, cancellationToken);
        return result.Status switch
        {
            CompletionStatus.Completed => TypedResults.Ok(TaskResponse.From(result.Task!)),
            CompletionStatus.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The task is already completed or was modified concurrently.")
        };
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, TasksService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
}
