using System.Text.Json;
using Microsoft.Extensions.Options;
using PANiXiDA.TaskApi.Tasks.Application.Tasks.Complete;
using RabbitMQ.Client;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

internal sealed class RabbitMqTransport(IOptions<RabbitMqOptions> options) : ITaskEventTransport
{
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task SendAsync(TaskCompleted message, CancellationToken cancellationToken)
    {
        if (_channel is not { IsOpen: true })
        {
            await CloseAsync(cancellationToken);
            var configuration = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = configuration.HostName,
                Port = configuration.Port,
                UserName = configuration.UserName,
                Password = configuration.Password,
                VirtualHost = configuration.VirtualHost,
                AutomaticRecoveryEnabled = false,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(2),
                ContinuationTimeout = TimeSpan.FromSeconds(2),
                ClientProvidedName = "task-api-publisher"
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        await _channel.ExchangeDeclareAsync(RabbitMqOptions.Exchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = message.TaskId.ToString(),
            Type = RabbitMqOptions.RoutingKey
        };
        await _channel.BasicPublishAsync(RabbitMqOptions.Exchange, RabbitMqOptions.RoutingKey,
            mandatory: false, properties, JsonSerializer.SerializeToUtf8Bytes(message, TaskEventJson.Options),
            cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                try
                {
                    if (channel.IsOpen)
                    {
                        await channel.CloseAsync(cancellationToken);
                    }
                }
                finally
                {
                    await channel.DisposeAsync().AsTask().WaitAsync(cancellationToken);
                }
            }
        }
        finally
        {
            if (connection is not null)
            {
                try
                {
                    if (connection.IsOpen)
                    {
                        await connection.CloseAsync(cancellationToken);
                    }
                }
                finally
                {
                    await connection.DisposeAsync().AsTask().WaitAsync(cancellationToken);
                }
            }
        }
    }
}
