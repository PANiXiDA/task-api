using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Complete;

internal static class CompleteTaskEndpoint
{
    internal static void Map(RouteGroupBuilder endpoints) =>
        endpoints.MapPut("/{id}/complete", HandleAsync).WithName("CompleteTask");

    private static async Task<Results<Ok<CompleteTaskResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id, CompleteTaskHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(CompleteTaskMapper.ToCommand(id), cancellationToken);
        return result.Status switch
        {
            CompletionStatus.Completed => TypedResults.Ok(CompleteTaskMapper.ToResponse(result.Task!)),
            CompletionStatus.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The task is already completed or was modified concurrently.")
        };
    }
}
