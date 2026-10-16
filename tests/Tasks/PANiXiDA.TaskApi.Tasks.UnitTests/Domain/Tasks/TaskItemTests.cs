using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.UnitTests.Domain.Tasks;

public sealed class TaskItemTests
{
    [Fact(DisplayName = "Создание задачи сохраняет название и приоритет и переводит время в UTC")]
    public void Create_Should_PreserveDetailsAndNormalizeTime_When_ValuesAreValid()
    {
        var createdAt = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(4));

        var task = TaskItem.Create("  Купить молоко  ", Priority.High, createdAt);

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("  Купить молоко  ", task.Title);
        Assert.Equal(Priority.High, task.Priority);
        Assert.Equal(createdAt.ToUniversalTime(), task.CreatedAt);
        Assert.Equal(TimeSpan.Zero, task.CreatedAt.Offset);
        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedAt);
    }

    [Theory(DisplayName = "Создание задачи отклоняет пустое или пробельное название")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("\u00a0\u2003\u2028")]
    public void Create_Should_RejectTitle_When_TitleIsBlank(string? title)
    {
        Assert.ThrowsAny<ArgumentException>(() => TaskItem.Create(title!, Priority.Medium, DateTimeOffset.UnixEpoch));
    }

    [Fact(DisplayName = "Название длиной 200 символов допустимо, а 201 символ — нет")]
    public void Create_Should_EnforceTitleLimit_When_TitleIsAtBoundary()
    {
        var task = TaskItem.Create(new string('я', 200), Priority.Low, DateTimeOffset.UnixEpoch);

        Assert.Equal(200, task.Title.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TaskItem.Create(new string('я', 201), Priority.Low, DateTimeOffset.UnixEpoch));
    }

    [Theory(DisplayName = "Создание задачи отклоняет неизвестный приоритет")]
    [InlineData(-1)]
    [InlineData(3)]
    public void Create_Should_RejectPriority_When_ValueIsUndefined(int priority)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TaskItem.Create("Задача", (Priority)priority, DateTimeOffset.UnixEpoch));
    }

    [Fact(DisplayName = "Первое завершение устанавливает время UTC, повторное сохраняет исходное время")]
    public void TryComplete_Should_PreserveFirstCompletion_When_CalledTwice()
    {
        var task = TaskItem.Create("Задача", Priority.Medium, DateTimeOffset.UnixEpoch);
        var completedAt = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(4));

        var first = task.TryComplete(completedAt);
        var repeated = task.TryComplete(completedAt.AddHours(1));

        Assert.True(first);
        Assert.False(repeated);
        Assert.True(task.IsCompleted);
        Assert.Equal(completedAt.ToUniversalTime(), task.CompletedAt);
        Assert.NotNull(task.CompletedAt);
        Assert.Equal(TimeSpan.Zero, task.CompletedAt.Value.Offset);
    }
}
