using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks;

public enum CompletionStatus
{
    Completed,
    NotFound,
    Conflict
}

public sealed record CompleteTaskResult(CompletionStatus Status, TaskItem? Task = null);
