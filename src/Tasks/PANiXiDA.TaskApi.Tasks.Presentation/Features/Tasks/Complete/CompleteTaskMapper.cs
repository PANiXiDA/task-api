using PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Complete;

internal static class CompleteTaskMapper
{
    internal static CompleteTaskCommand ToCommand(Guid id) => new(id);

    internal static CompleteTaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.IsCompleted, task.CreatedAt, task.CompletedAt, task.Priority);
}
