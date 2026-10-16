using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Complete;
using PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Create;
using PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Delete;
using PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.GetList;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasks(this IEndpointRouteBuilder endpoints)
    {
        var tasks = endpoints.MapGroup("/tasks");
        CreateTaskEndpoint.Map(tasks);
        GetTasksEndpoint.Map(tasks);
        CompleteTaskEndpoint.Map(tasks);
        DeleteTaskEndpoint.Map(tasks);
        return endpoints;
    }
}
