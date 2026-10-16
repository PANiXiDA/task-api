using NSubstitute;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Create;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.UnitTests.Application.Tasks.Create;

public sealed class CreateTaskHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1).AddTicks(1234567);

    [Fact(DisplayName = "Создание использует часы обработчика и сохраняет время с точностью до микросекунды")]
    public async Task HandleAsync_Should_SaveMicrosecondTimestamp_When_TaskIsValid()
    {
        var token = TestContext.Current.CancellationToken;
        var repository = Substitute.For<ITasksRepository>();
        var handler = new CreateTaskHandler(repository, new FixedTimeProvider());

        var task = await handler.HandleAsync(new CreateTaskCommand("Задача", Priority.Low), token);

        Assert.Equal(Now.AddTicks(-7), task.CreatedAt);
        await repository.Received(1).AddAsync(task, token);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
