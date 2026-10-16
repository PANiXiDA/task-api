using PANiXiDA.TaskApi.Tasks.Application.Tasks.Delete;

namespace PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks.Delete;

internal static class DeleteTaskMapper
{
    internal static DeleteTaskCommand ToCommand(Guid id) => new(id);
}
