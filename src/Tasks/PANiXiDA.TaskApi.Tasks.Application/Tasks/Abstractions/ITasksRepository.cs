using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;

public interface ITasksRepository
{
    Task AddAsync(TaskItem task, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
