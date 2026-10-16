using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.IntegrationTests.Infrastructure.Persistence;

public sealed class TasksMigrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact(DisplayName = "Повторное применение миграции сохраняет существующие задачи")]
    public async Task Migrate_Should_PreserveData_When_AppliedAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = Fixture.CreateScope();
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        await scope.ServiceProvider.GetRequiredService<ITasksRepository>().AddAsync(task, token);
        var context = scope.ServiceProvider.GetRequiredService<TasksDbContext>();

        await context.Database.MigrateAsync(token);

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
        Assert.Equal(task.Id, (await context.Tasks.AsNoTracking().SingleAsync(token)).Id);
    }

    [Theory(DisplayName = "Ограничения PostgreSQL отклоняют некорректные данные при прямой записи")]
    [InlineData("UPDATE tasks SET \"Title\" = ' '", "23514")]
    [InlineData("UPDATE tasks SET \"Title\" = repeat('a', 201)", "22001")]
    [InlineData("UPDATE tasks SET \"Priority\" = 'Other'", "23514")]
    [InlineData("UPDATE tasks SET \"IsCompleted\" = true", "23514")]
    public async Task Constraints_Should_RejectInvalidData_When_BypassingDomain(string sql, string sqlState)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = Fixture.CreateScope();
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        await scope.ServiceProvider.GetRequiredService<ITasksRepository>().AddAsync(task, token);
        var context = scope.ServiceProvider.GetRequiredService<TasksDbContext>();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, token));

        Assert.Equal(sqlState, exception.SqlState);
        Assert.False((await context.Tasks.AsNoTracking().SingleAsync(token)).IsCompleted);
    }
}
