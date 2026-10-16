using Microsoft.EntityFrameworkCore;
using PANiXiDA.TaskApi.Testing;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.FunctionalTests;

public sealed class FunctionalTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new();
    public RabbitMqTestBroker Broker { get; } = new();

    public Dictionary<string, string?> Configuration
    {
        get
        {
            var configuration = Broker.GetConfiguration();
            configuration["ConnectionStrings:Tasks"] = _database.ConnectionString;
            return configuration;
        }
    }

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await Task.WhenAll(_database.InitializeAsync(token), Broker.InitializeAsync(token));
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync(token);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateDbContext();
        await context.Tasks.ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Broker.DisposeAsync();
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    private TasksDbContext CreateDbContext() => new(new DbContextOptionsBuilder<TasksDbContext>()
        .UseNpgsql(_database.ConnectionString).Options);
}
