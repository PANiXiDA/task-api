using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Common;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Create;

public sealed class CreateTaskHandler(ITasksRepository repository, TimeProvider timeProvider)
{
    public async Task<TaskItem> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var task = TaskItem.Create(command.Title, command.Priority, TaskTimestamp.GetUtcNow(timeProvider));
        await repository.AddAsync(task, cancellationToken);
        return task;
    }
}
