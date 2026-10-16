using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

namespace PANiXiDA.TaskApi.Tasks.UnitTests.Infrastructure.Messaging;

public sealed class TaskEventPublisherTests
{
    [Fact(DisplayName = "Остановка отправляет начатое и ожидающие события до закрытия соединения")]
    public async Task Stop_Should_DrainStartedAndQueuedPublications_Before_ClosingTransport()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new RecordingTransport(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var publisher = CreatePublisher(transport);
        await publisher.StartAsync(TestContext.Current.CancellationToken);
        var first = Message();
        var second = Message();
        publisher.Publish(first);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        publisher.Publish(second);

        var stopping = publisher.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(stopping.IsCompleted);
        Assert.False(transport.Closed);
        release.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([first, second], transport.Messages.ToArray());
        Assert.True(transport.Closed);
    }

    [Fact(DisplayName = "Ошибка отправки записывает событие в лог и не мешает следующей публикации")]
    public async Task Publish_Should_LogPayloadAndContinue_When_TransportFails()
    {
        var first = Message();
        var second = Message();
        var logger = new RecordingLogger();
        var transport = new RecordingTransport((message, _) =>
            message == first ? Task.FromException(new IOException("Broker unavailable")) : Task.CompletedTask);
        using var publisher = CreatePublisher(transport, logger);
        await publisher.StartAsync(TestContext.Current.CancellationToken);

        publisher.Publish(first);
        publisher.Publish(second);
        await publisher.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(second, Assert.Single(transport.Messages));
        Assert.Contains(logger.Messages, x => x.Contains(first.TaskId.ToString(), StringComparison.Ordinal)
            && x.Contains(first.Title, StringComparison.Ordinal));
        Assert.True(transport.Closed);
    }

    [Fact(DisplayName = "Переполнение или остановка очереди записывает неотправленное событие в лог")]
    public async Task Publish_Should_LogDroppedEvent_When_QueueIsFullOrStopped()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new RecordingLogger();
        var transport = new RecordingTransport(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var publisher = CreatePublisher(transport, logger, capacity: 1);
        await publisher.StartAsync(TestContext.Current.CancellationToken);
        publisher.Publish(Message());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        publisher.Publish(Message());
        var overflow = Message();

        publisher.Publish(overflow);
        release.SetResult();
        await publisher.StopAsync(TestContext.Current.CancellationToken);
        var afterStop = Message();
        publisher.Publish(afterStop);

        Assert.Equal(2, transport.Messages.Count);
        Assert.Contains(logger.Messages, x => x.Contains(overflow.TaskId.ToString(), StringComparison.Ordinal));
        Assert.Contains(logger.Messages, x => x.Contains(afterStop.TaskId.ToString(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Остановка отменяет зависшие публикации и закрывает соединение за пять секунд")]
    public async Task Stop_Should_CancelHungPublicationsAndClose_Within_ShutdownBudget()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new RecordingTransport(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var logger = new RecordingLogger();
        using var publisher = CreatePublisher(transport, logger);
        await publisher.StartAsync(TestContext.Current.CancellationToken);
        var events = Enumerable.Range(0, 5).Select(_ => Message()).ToArray();
        foreach (var message in events)
        {
            publisher.Publish(message);
        }

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var elapsed = Stopwatch.StartNew();
        await publisher.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {elapsed.Elapsed}.");
        Assert.True(transport.Closed);
        Assert.Empty(transport.Messages);
        Assert.All(events, message => Assert.Contains(logger.Messages,
            x => x.Contains(message.TaskId.ToString(), StringComparison.Ordinal)));
    }

    private static TaskEventPublisher CreatePublisher(
        ITaskEventTransport transport, RecordingLogger? logger = null, int capacity = 16) =>
        new(transport, Options.Create(new RabbitMqOptions { QueueCapacity = capacity }), logger ?? new RecordingLogger());

    private static TaskCompleted Message() => new(Guid.NewGuid(), "Buy milk", DateTimeOffset.UtcNow, Priority.High);

    private sealed class RecordingTransport(Func<TaskCompleted, CancellationToken, Task> send) : ITaskEventTransport
    {
        public ConcurrentQueue<TaskCompleted> Messages { get; } = new();
        public bool Closed { get; private set; }

        public async Task SendAsync(TaskCompleted message, CancellationToken cancellationToken)
        {
            Assert.False(Closed);
            await send(message, cancellationToken);
            Messages.Enqueue(message);
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            Closed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger<TaskEventPublisher>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
    }
}
