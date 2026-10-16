using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Create;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Create;

internal static class CreateTaskEndpoint
{
    internal static void Map(RouteGroupBuilder endpoints) =>
        endpoints.MapPost("/", HandleAsync).WithName("CreateTask");

    private static async Task<Results<Created<CreateTaskResponse>, ValidationProblem>> HandleAsync(
        CreateTaskRequest request, CreateTaskHandler handler, CancellationToken cancellationToken)
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

        var task = await handler.HandleAsync(CreateTaskMapper.ToCommand(request), cancellationToken);
        return TypedResults.Created("/tasks", CreateTaskMapper.ToResponse(task));
    }
}
