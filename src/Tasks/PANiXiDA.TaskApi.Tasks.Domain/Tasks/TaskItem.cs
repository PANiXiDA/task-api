namespace PANiXiDA.TaskApi.Tasks.Domain.Tasks;

public sealed class TaskItem
{
    public const int MaxTitleLength = 200;

    private TaskItem() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Priority Priority { get; private set; }
    public uint RowVersion { get; private set; }

    public static TaskItem Create(string title, Priority priority, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
        {
            throw new ArgumentOutOfRangeException(nameof(title));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Priority = priority,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public bool TryComplete(DateTimeOffset completedAt)
    {
        if (IsCompleted)
        {
            return false;
        }

        IsCompleted = true;
        CompletedAt = completedAt.ToUniversalTime();
        return true;
    }
}
