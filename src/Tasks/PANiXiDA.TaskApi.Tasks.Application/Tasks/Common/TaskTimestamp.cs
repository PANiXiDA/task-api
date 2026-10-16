namespace PANiXiDA.TaskApi.Tasks.Application.Tasks.Common;

internal static class TaskTimestamp
{
    internal static DateTimeOffset GetUtcNow(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
