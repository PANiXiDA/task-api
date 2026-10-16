using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.IntegrationTests.Infrastructure.Persistence;

public sealed class TasksRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact(DisplayName = "Репозиторий сохраняет задачу в PostgreSQL с UTC и версией xmin")]
    public async Task Add_Should_PersistAllFields_When_TaskIsValid()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.High, DateTimeOffset.UnixEpoch.AddHours(1));
        await using (var write = Fixture.CreateScope())
        {
            await write.ServiceProvider.GetRequiredService<ITasksRepository>().AddAsync(task, token);
        }

        await using var read = Fixture.CreateScope();
        var saved = await read.ServiceProvider.GetRequiredService<ITasksRepository>().FindAsync(task.Id, token);

        Assert.NotNull(saved);
        Assert.Equal(task.Title, saved.Title);
        Assert.Equal(task.Priority, saved.Priority);
        Assert.Equal(task.CreatedAt, saved.CreatedAt);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
        Assert.False(saved.IsCompleted);
        Assert.Null(saved.CompletedAt);
        Assert.NotEqual(0u, saved.RowVersion);
    }

    [Fact(DisplayName = "Список задач сортируется по времени и ID и не отслеживается контекстом")]
    public async Task GetAll_Should_ReturnOrderedUntrackedTasks_When_TimestampsMatch()
    {
        var token = TestContext.Current.CancellationToken;
        var first = TaskItem.Create("Первая", Priority.Low, DateTimeOffset.UnixEpoch);
        var second = TaskItem.Create("Вторая", Priority.Medium, DateTimeOffset.UnixEpoch);
        var last = TaskItem.Create("Последняя", Priority.High, DateTimeOffset.UnixEpoch.AddSeconds(1));
        await using (var write = Fixture.CreateScope())
        {
            var repository = write.ServiceProvider.GetRequiredService<ITasksRepository>();
            foreach (var task in new[] { last, second, first })
            {
                await repository.AddAsync(task, token);
            }
        }

        await using var read = Fixture.CreateScope();
        var tasks = await read.ServiceProvider.GetRequiredService<ITasksRepository>().GetAllAsync(token);

        Assert.Equal(new[] { first, second, last }.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id), tasks.Select(x => x.Id));
        Assert.Empty(read.ServiceProvider.GetRequiredService<TasksDbContext>().ChangeTracker.Entries());
    }

    [Fact(DisplayName = "Конкурентное сохранение одной версии xmin допускает только одно завершение")]
    public async Task TrySave_Should_RejectStaleVersion_When_TwoContextsCompleteSameTask()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        await using (var seed = Fixture.CreateScope())
        {
            await seed.ServiceProvider.GetRequiredService<ITasksRepository>().AddAsync(task, token);
        }

        await using var firstScope = Fixture.CreateScope();
        await using var secondScope = Fixture.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<ITasksRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<ITasksRepository>();
        var first = await firstRepository.FindAsync(task.Id, token);
        var second = await secondRepository.FindAsync(task.Id, token);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.RowVersion, second.RowVersion);
        first.TryComplete(DateTimeOffset.UnixEpoch.AddHours(1));
        second.TryComplete(DateTimeOffset.UnixEpoch.AddHours(2));

        var savedFirst = await firstRepository.TrySaveAsync(token);
        var savedSecond = await secondRepository.TrySaveAsync(token);

        Assert.True(savedFirst);
        Assert.False(savedSecond);
        await using var read = Fixture.CreateScope();
        var saved = await read.ServiceProvider.GetRequiredService<ITasksRepository>().FindAsync(task.Id, token);
        Assert.NotNull(saved);
        Assert.Equal(first.CompletedAt, saved.CompletedAt);
        Assert.NotEqual(second.RowVersion, saved.RowVersion);
    }

    [Fact(DisplayName = "Удаление строки делает ранее прочитанную версию непригодной для сохранения")]
    public async Task TrySave_Should_ReturnConflict_When_TaskWasDeletedAfterRead()
    {
        var token = TestContext.Current.CancellationToken;
        var task = TaskItem.Create("Задача", Priority.Low, DateTimeOffset.UnixEpoch);
        await using var write = Fixture.CreateScope();
        var repository = write.ServiceProvider.GetRequiredService<ITasksRepository>();
        await repository.AddAsync(task, token);
        await using var deletion = Fixture.CreateScope();
        var deletingRepository = deletion.ServiceProvider.GetRequiredService<ITasksRepository>();

        var deleted = await deletingRepository.DeleteAsync(task.Id, token);
        task.TryComplete(DateTimeOffset.UnixEpoch.AddHours(1));
        var saved = await repository.TrySaveAsync(token);

        Assert.True(deleted);
        Assert.False(saved);
        Assert.Null(await deletingRepository.FindAsync(task.Id, token));
        Assert.False(await deletingRepository.DeleteAsync(task.Id, token));
    }
}
