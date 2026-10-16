using PANiXiDA.TaskApi.Tasks.Application.Tasks.GetList;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.GetList;

internal static class GetTasksMapper
{
    internal static GetTasksQuery ToQuery() => new();

    internal static TaskListItemResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.IsCompleted, task.CreatedAt, task.CompletedAt, task.Priority);
}
