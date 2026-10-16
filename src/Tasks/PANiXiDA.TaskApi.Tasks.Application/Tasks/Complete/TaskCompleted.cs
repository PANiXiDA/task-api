using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;

public sealed record TaskCompleted(Guid TaskId, string Title, DateTimeOffset CompletedAt, Priority Priority);
