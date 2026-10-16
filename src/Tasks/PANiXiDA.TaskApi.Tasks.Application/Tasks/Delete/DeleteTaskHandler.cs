using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Delete;

public sealed class DeleteTaskHandler(ITasksRepository repository)
{
    public Task<bool> HandleAsync(DeleteTaskCommand command, CancellationToken cancellationToken) =>
        repository.DeleteAsync(command.Id, cancellationToken);
}
