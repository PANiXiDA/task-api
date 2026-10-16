using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Create;

public sealed record CreateTaskRequest(string? Title, Priority Priority = Priority.Medium);
