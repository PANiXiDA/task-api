using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.GetList;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.GetList;

internal static class GetTasksEndpoint
{
    internal static void Map(RouteGroupBuilder endpoints) =>
        endpoints.MapGet("/", HandleAsync).WithName("GetTasks");

    private static async Task<Ok<TaskListItemResponse[]>> HandleAsync(GetTasksHandler handler, CancellationToken cancellationToken)
    {
        var tasks = await handler.HandleAsync(GetTasksMapper.ToQuery(), cancellationToken);
        return TypedResults.Ok(tasks.Select(GetTasksMapper.ToResponse).ToArray());
    }
}
