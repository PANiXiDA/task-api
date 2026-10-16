using NSubstitute;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.UnitTests.Application.Tasks;

public sealed class TasksServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1).AddTicks(1234567);
    private readonly ITasksRepository _repository = Substitute.For<ITasksRepository>();
    private readonly ITaskEventPublisher _publisher = Substitute.For<ITaskEventPublisher>();

    [Fact(DisplayName = "Создание использует часы сервиса и сохраняет время с точностью до микросекунды")]
    public async Task Create_Should_SaveMicrosecondTimestamp_When_TaskIsValid()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService();

        var task = await service.CreateAsync("Задача", Priority.Low, token);

        Assert.Equal(Now.AddTicks(-7), task.CreatedAt);
        await _repository.Received(1).AddAsync(task, token);
        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    [Fact(DisplayName = "Завершение публикует сохранённые данные только после успешного сохранения")]
    public async Task Complete_Should_PublishAfterSave_When_SaveSucceeds()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.High, DateTimeOffset.UnixEpoch);
        var saved = false;
        _repository.FindAsync(task.Id, token).Returns(task);
        _repository.TrySaveAsync(token).Returns(_ =>
        {
            saved = true;
            return true;
        });
        _publisher.When(x => x.Publish(Arg.Any<TaskCompleted>())).Do(_ => Assert.True(saved));

        var result = await CreateService().CompleteAsync(task.Id, token);

        Assert.Equal(CompletionStatus.Completed, result.Status);
        Assert.Same(task, result.Task);
        Assert.Equal(Now.AddTicks(-7), task.CompletedAt);
        Assert.NotNull(task.CompletedAt);
        _publisher.Received(1).Publish(new TaskCompleted(task.Id, task.Title, task.CompletedAt.Value, task.Priority));
    }

    [Fact(DisplayName = "Завершение отсутствующей задачи не сохраняет и не публикует событие")]
    public async Task Complete_Should_ReturnNotFound_When_TaskDoesNotExist()
    {
        var token = TestContext.Current.CancellationToken;

        var result = await CreateService().CompleteAsync(Guid.NewGuid(), token);

        Assert.Equal(CompletionStatus.NotFound, result.Status);
        Assert.Null(result.Task);
        await _repository.DidNotReceive().TrySaveAsync(Arg.Any<CancellationToken>());
        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    [Fact(DisplayName = "Повторное завершение возвращает конфликт без сохранения и публикации")]
    public async Task Complete_Should_ReturnConflict_When_TaskIsAlreadyCompleted()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        task.TryComplete(DateTimeOffset.UnixEpoch.AddHours(1));
        _repository.FindAsync(task.Id, token).Returns(task);

        var result = await CreateService().CompleteAsync(task.Id, token);

        Assert.Equal(CompletionStatus.Conflict, result.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(1), task.CompletedAt);
        await _repository.DidNotReceive().TrySaveAsync(Arg.Any<CancellationToken>());
        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    [Fact(DisplayName = "Конфликт версии строки предотвращает публикацию события")]
    public async Task Complete_Should_NotPublish_When_RepositoryReportsConcurrencyConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        _repository.FindAsync(task.Id, token).Returns(task);
        _repository.TrySaveAsync(token).Returns(false);

        var result = await CreateService().CompleteAsync(task.Id, token);

        Assert.Equal(CompletionStatus.Conflict, result.Status);
        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    [Fact(DisplayName = "Ошибка сохранения передаётся вызывающему коду без публикации события")]
    public async Task Complete_Should_PropagateOriginalFailure_When_SaveThrows()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        var failure = new InvalidOperationException("Ошибка сохранения");
        _repository.FindAsync(task.Id, token).Returns(task);
        _repository.TrySaveAsync(token).Returns(Task.FromException<bool>(failure));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CompleteAsync(task.Id, token));

        Assert.Same(failure, actual);
        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    [Fact(DisplayName = "Отмена сохранения не приводит к публикации события")]
    public async Task Complete_Should_NotPublish_When_SaveIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        _repository.FindAsync(task.Id, cancellation.Token).Returns(task);
        _repository.TrySaveAsync(cancellation.Token).Returns(Task.FromCanceled<bool>(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().CompleteAsync(task.Id, cancellation.Token));

        _publisher.DidNotReceive().Publish(Arg.Any<TaskCompleted>());
    }

    private TasksService CreateService() => new(_repository, _publisher, new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
