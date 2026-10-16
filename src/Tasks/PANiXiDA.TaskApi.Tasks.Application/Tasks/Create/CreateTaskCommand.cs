using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Create;

public sealed record CreateTaskCommand(string Title, Priority Priority);
