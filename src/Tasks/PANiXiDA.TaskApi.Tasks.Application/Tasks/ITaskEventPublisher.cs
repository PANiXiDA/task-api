namespace PANiXiDA.TaskApi.Tasks.Application.Tasks;

public interface ITaskEventPublisher
{
    void Publish(TaskCompleted message);
}
