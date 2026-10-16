using Testcontainers.PostgreSql;

namespace PANiXiDA.TaskApi.Testing;

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tasks_tests")
        .WithUsername("tasks")
        .WithPassword("isolated-test-only")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync(CancellationToken cancellationToken) => _container.StartAsync(cancellationToken);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
