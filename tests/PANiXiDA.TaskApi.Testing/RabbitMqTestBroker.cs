using System.Globalization;
using Testcontainers.RabbitMq;

namespace PANiXiDA.TaskApi.Testing;

public sealed class RabbitMqTestBroker : IAsyncDisposable
{
    public const string UserName = "tasks";
    public const string Password = "isolated-test-only";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.1-alpine")
        .WithUsername(UserName)
        .WithPassword(Password)
        .Build();

    public string HostName => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5672);

    public Dictionary<string, string?> GetConfiguration() => new()
    {
        ["RabbitMq:HostName"] = HostName,
        ["RabbitMq:Port"] = Port.ToString(CultureInfo.InvariantCulture),
        ["RabbitMq:UserName"] = UserName,
        ["RabbitMq:Password"] = Password
    };

    public Task InitializeAsync(CancellationToken cancellationToken) => _container.StartAsync(cancellationToken);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
