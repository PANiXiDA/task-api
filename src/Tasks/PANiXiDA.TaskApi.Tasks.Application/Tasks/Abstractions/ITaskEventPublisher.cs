using PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;

namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;

public interface ITaskEventPublisher
{
    void Publish(TaskCompleted message);
}
