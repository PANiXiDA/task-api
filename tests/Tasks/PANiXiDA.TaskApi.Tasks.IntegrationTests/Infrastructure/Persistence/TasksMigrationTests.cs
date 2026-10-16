using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Abstractions;
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

    [Fact(DisplayName = "Обновление существующей БД удаляет CHECK-ограничения и сохраняет задачи")]
    public async Task Migrate_Should_RemoveCheckConstraintsAndPreserveData_When_UpgradingInitialSchema()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
        await context.GetService<IMigrator>().MigrateAsync("20261016162834_InitialCreate", token);
        var task = TaskItem.Create("Существующая задача", Priority.High, DateTimeOffset.UnixEpoch);
        await scope.ServiceProvider.GetRequiredService<ITasksRepository>().AddAsync(task, token);
        const string countConstraintsSql = "SELECT count(*)::integer AS \"Value\" FROM pg_constraint WHERE conrelid = 'tasks'::regclass AND contype = 'c'";
        Assert.Equal(3, await context.Database.SqlQueryRaw<int>(countConstraintsSql).SingleAsync(token));

        await context.Database.MigrateAsync(token);

        Assert.Equal(0, await context.Database.SqlQueryRaw<int>(countConstraintsSql).SingleAsync(token));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
        var saved = await context.Tasks.AsNoTracking().SingleAsync(token);
        Assert.Equal(task.Id, saved.Id);
        Assert.Equal(task.Title, saved.Title);
        Assert.Equal(task.Priority, saved.Priority);
        Assert.Equal(task.CreatedAt, saved.CreatedAt);
    }

    [Theory(DisplayName = "PostgreSQL отклоняет NULL и превышение длины названия при прямой записи")]
    [InlineData("UPDATE tasks SET \"Title\" = NULL", "23502")]
    [InlineData("UPDATE tasks SET \"Title\" = repeat('a', 201)", "22001")]
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
