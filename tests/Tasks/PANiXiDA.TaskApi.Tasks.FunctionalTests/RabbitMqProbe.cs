using System.Text.Json;
using System.Text.Json.Serialization;
using PANiXiDA.TaskApi.Testing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PANiXiDA.TaskApi.Tasks.FunctionalTests;

internal sealed class RabbitMqProbe(IConnection connection, IChannel channel, string queue) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<RabbitMqProbe> CreateAsync(RabbitMqTestBroker broker)
    {
        var factory = new ConnectionFactory
        {
            HostName = broker.HostName,
            Port = broker.Port,
            UserName = RabbitMqTestBroker.UserName,
            Password = RabbitMqTestBroker.Password,
            AutomaticRecoveryEnabled = false
        };
        var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.ExchangeDeclareAsync(RabbitMqOptions.Exchange, ExchangeType.Topic, durable: true, cancellationToken: TestContext.Current.CancellationToken);
        var queue = await channel.QueueDeclareAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.QueueBindAsync(queue.QueueName, RabbitMqOptions.Exchange, RabbitMqOptions.RoutingKey, cancellationToken: TestContext.Current.CancellationToken);
        return new RabbitMqProbe(connection, channel, queue.QueueName);
    }

    public async Task<TaskCompleted> ReadAsync(Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var delivery = await channel.BasicGetAsync(queue, autoAck: true, timeout.Token);
            if (delivery is not null)
            {
                Assert.Equal(RabbitMqOptions.RoutingKey, delivery.RoutingKey);
                Assert.Equal("application/json", delivery.BasicProperties.ContentType);
                using var json = JsonDocument.Parse(delivery.Body);
                Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("priority").ValueKind);
                var message = JsonSerializer.Deserialize<TaskCompleted>(delivery.Body.Span, JsonOptions)!;
                if (message.TaskId == id)
                {
                    return message;
                }
            }

            await Task.Delay(25, timeout.Token);
        }
    }

    public async Task AssertNoMessageAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Null(await channel.BasicGetAsync(queue, autoAck: true, timeout.Token));
    }

    public async ValueTask DisposeAsync()
    {
        await channel.CloseAsync(TestContext.Current.CancellationToken);
        await channel.DisposeAsync();
        await connection.CloseAsync(TestContext.Current.CancellationToken);
        await connection.DisposeAsync();
    }
}
