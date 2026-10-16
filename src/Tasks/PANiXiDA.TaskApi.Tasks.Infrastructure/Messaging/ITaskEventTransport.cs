using PANiXiDA.TaskApi.Tasks.Application.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

public interface ITaskEventTransport
{
    Task SendAsync(TaskCompleted message, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}
