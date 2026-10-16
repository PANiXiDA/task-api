using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

public sealed class TaskEventPublisher(
    ITaskEventTransport transport,
    IOptions<RabbitMqOptions> options,
    ILogger<TaskEventPublisher> logger) : BackgroundService, ITaskEventPublisher
{
    private readonly Channel<TaskCompleted> _queue = Channel.CreateBounded<TaskCompleted>(
        new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    public void Publish(TaskCompleted message)
    {
        if (!_queue.Writer.TryWrite(message))
        {
            LogUnsent(message, "Publisher queue is full or stopping");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await transport.SendAsync(message, timeout.Token);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Task completion event was not sent: {Message}",
                        JsonSerializer.Serialize(message, TaskEventJson.Options));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            while (_queue.Reader.TryRead(out var message))
            {
                LogUnsent(message, "Publisher shutdown timeout");
            }

            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                await transport.CloseAsync(closeTimeout.Token);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "RabbitMQ connection cleanup did not complete normally");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        using var drainTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainTimeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            if (ExecuteTask is not null)
            {
                await ExecuteTask.WaitAsync(drainTimeout.Token);
            }
        }
        catch (OperationCanceledException) when (drainTimeout.IsCancellationRequested)
        {
            logger.LogWarning("Task event publisher drain deadline reached");
        }

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await base.StopAsync(stopTimeout.Token);
    }

    private void LogUnsent(TaskCompleted message, string reason) =>
        logger.LogWarning("{Reason}. Task completion event was not sent: {Message}", reason,
            JsonSerializer.Serialize(message, TaskEventJson.Options));
}
