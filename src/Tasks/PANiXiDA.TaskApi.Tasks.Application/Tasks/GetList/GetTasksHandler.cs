using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.GetList;

public sealed class GetTasksHandler(ITasksRepository repository)
{
    public Task<IReadOnlyList<TaskItem>> HandleAsync(GetTasksQuery query, CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);
}
