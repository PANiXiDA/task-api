using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks;

public sealed class TasksService(
    ITasksRepository repository,
    ITaskEventPublisher publisher,
    TimeProvider timeProvider)
{
    public async Task<TaskItem> CreateAsync(string title, Priority priority, CancellationToken cancellationToken)
    {
        var task = TaskItem.Create(title, priority, GetTimestamp());
        await repository.AddAsync(task, cancellationToken);
        return task;
    }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<CompleteTaskResult> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await repository.FindAsync(id, cancellationToken);
        if (task is null)
        {
            return new CompleteTaskResult(CompletionStatus.NotFound);
        }

        if (!task.TryComplete(GetTimestamp()) || !await repository.TrySaveAsync(cancellationToken))
        {
            return new CompleteTaskResult(CompletionStatus.Conflict);
        }

        publisher.Publish(new TaskCompleted(task.Id, task.Title, task.CompletedAt!.Value, task.Priority));
        return new CompleteTaskResult(CompletionStatus.Completed, task);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);

    private DateTimeOffset GetTimestamp()
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
