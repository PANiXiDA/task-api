using PANiXiDA.TaskApi.Tasks.Application.Tasks.Create;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Create;

internal static class CreateTaskMapper
{
    internal static CreateTaskCommand ToCommand(CreateTaskRequest request) => new(request.Title!, request.Priority);

    internal static CreateTaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.IsCompleted, task.CreatedAt, task.CompletedAt, task.Priority);
}
