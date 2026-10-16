using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Common;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;

public sealed class CompleteTaskHandler(
    ITasksRepository repository,
    ITaskEventPublisher publisher,
    TimeProvider timeProvider)
{
    public async Task<CompleteTaskResult> HandleAsync(CompleteTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await repository.FindAsync(command.Id, cancellationToken);
        if (task is null)
        {
            return new CompleteTaskResult(CompletionStatus.NotFound);
        }

        if (!task.TryComplete(TaskTimestamp.GetUtcNow(timeProvider)) || !await repository.TrySaveAsync(cancellationToken))
        {
            return new CompleteTaskResult(CompletionStatus.Conflict);
        }

        publisher.Publish(new TaskCompleted(task.Id, task.Title, task.CompletedAt!.Value, task.Priority));
        return new CompleteTaskResult(CompletionStatus.Completed, task);
    }
}
