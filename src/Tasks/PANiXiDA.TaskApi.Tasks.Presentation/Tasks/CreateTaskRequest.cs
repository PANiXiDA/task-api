using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Tasks;

public sealed record CreateTaskRequest(string? Title, Priority Priority = Priority.Medium);
