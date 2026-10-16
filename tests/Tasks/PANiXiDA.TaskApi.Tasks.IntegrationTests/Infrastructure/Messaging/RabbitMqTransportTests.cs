using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Testing;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PANiXiDA.TaskApi.Tasks.IntegrationTests.Infrastructure.Messaging;

public sealed class RabbitMqTransportTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact(DisplayName = "Транспорт отправляет persistent JSON в настоящий RabbitMQ и восстанавливает закрытое соединение")]
    public async Task Send_Should_DeliverContractAndReconnect_When_TransportWasClosed()
    {
        var token = TestContext.Current.CancellationToken;
        var connectionFactory = new ConnectionFactory
        {
            HostName = Fixture.Broker.HostName,
            Port = Fixture.Broker.Port,
            UserName = RabbitMqTestBroker.UserName,
            Password = RabbitMqTestBroker.Password
        };
        await using var connection = await connectionFactory.CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
        await channel.ExchangeDeclareAsync(RabbitMqOptions.Exchange, ExchangeType.Topic, durable: true, cancellationToken: token);
        var queue = await channel.QueueDeclareAsync(cancellationToken: token);
        await channel.QueueBindAsync(queue.QueueName, RabbitMqOptions.Exchange, RabbitMqOptions.RoutingKey, cancellationToken: token);
        await using var scope = Fixture.CreateScope();
        var transport = scope.ServiceProvider.GetRequiredService<ITaskEventTransport>();

        foreach (var priority in new[] { Priority.High, Priority.Low })
        {
            var message = new TaskCompleted(Guid.NewGuid(), "Купить молоко", DateTimeOffset.UnixEpoch.AddDays(1), priority);
            await transport.SendAsync(message, token);
            var delivery = await ReadAsync(channel, queue.QueueName, token);

            Assert.Equal(RabbitMqOptions.Exchange, delivery.Exchange);
            Assert.Equal(RabbitMqOptions.RoutingKey, delivery.RoutingKey);
            Assert.Equal("application/json", delivery.BasicProperties.ContentType);
            Assert.True(delivery.BasicProperties.Persistent);
            Assert.Equal(message.TaskId.ToString(), delivery.BasicProperties.MessageId);
            Assert.Equal(RabbitMqOptions.RoutingKey, delivery.BasicProperties.Type);
            using var json = JsonDocument.Parse(delivery.Body);
            Assert.Equal(4, json.RootElement.EnumerateObject().Count());
            Assert.Equal(message.TaskId, json.RootElement.GetProperty("taskId").GetGuid());
            Assert.Equal(message.Title, json.RootElement.GetProperty("title").GetString());
            Assert.Equal(message.CompletedAt, json.RootElement.GetProperty("completedAt").GetDateTimeOffset());
            Assert.Equal(priority.ToString(), json.RootElement.GetProperty("priority").GetString());
            await transport.CloseAsync(token);
        }
    }

    private static async Task<BasicGetResult> ReadAsync(IChannel channel, string queue, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: true, timeout.Token);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(25, timeout.Token);
        }
    }
}
