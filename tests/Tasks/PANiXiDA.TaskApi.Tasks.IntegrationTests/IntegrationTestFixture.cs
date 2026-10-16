using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Testing;
using PANiXiDA.TaskApi.Tasks.Infrastructure.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new();
    private ServiceProvider? _services;

    public RabbitMqTestBroker Broker { get; } = new();
    public string ConnectionString => _database.ConnectionString;

    public AsyncServiceScope CreateScope() => (_services ?? throw new InvalidOperationException("Fixture is not initialized.")).CreateAsyncScope();

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await Task.WhenAll(_database.InitializeAsync(token), Broker.InitializeAsync(token));
        var settings = Broker.GetConfiguration();
        settings["ConnectionStrings:Tasks"] = ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database.MigrateAsync(token);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Tasks.ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_services is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _services.GetRequiredService<ITaskEventTransport>().CloseAsync(timeout.Token);
                }
                finally
                {
                    await _services.DisposeAsync();
                }
            }
        }
        finally
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
    }
}
