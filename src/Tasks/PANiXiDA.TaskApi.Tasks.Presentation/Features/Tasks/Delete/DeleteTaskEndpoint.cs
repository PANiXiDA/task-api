using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Delete;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Delete;

internal static class DeleteTaskEndpoint
{
    internal static void Map(RouteGroupBuilder endpoints) =>
        endpoints.MapDelete("/{id}", HandleAsync).WithName("DeleteTask");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid id, DeleteTaskHandler handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(DeleteTaskMapper.ToCommand(id), cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
