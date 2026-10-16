using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;
using PANiXiDA.TaskApi.Tasks.Presentation.Tasks;

namespace PANiXiDA.TaskApi.Tasks.FunctionalTests;

public sealed class TasksEndpointsTests(FunctionalTestFixture fixture) : IClassFixture<FunctionalTestFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async ValueTask InitializeAsync() => await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(DisplayName = "Завершение сохраняет задачу и публикует одно событие с данными из БД")]
    public async Task Complete_Should_PublishPersistedTask_When_TaskExists()
    {
        await using var probe = await RabbitMqProbe.CreateAsync(fixture.Broker);
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "Buy milk", "High");
        Assert.False(created.IsCompleted);
        Assert.Null(created.CompletedAt);
        Assert.Equal(Priority.High, created.Priority);

        using var completedResponse = await client.PutAsync($"/tasks/{created.Id}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = (await completedResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
        var message = await probe.ReadAsync(created.Id);
        var tasks = (await client.GetFromJsonAsync<TaskResponse[]>("/tasks", JsonOptions, TestContext.Current.CancellationToken))!;
        var saved = Assert.Single(tasks, x => x.Id == created.Id);

        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);
        Assert.True(saved.CompletedAt >= saved.CreatedAt);
        Assert.Equal(completed.CompletedAt, saved.CompletedAt);
        Assert.Equal(saved.CompletedAt, message.CompletedAt);
        Assert.Equal(saved.Title, message.Title);
        Assert.Equal(saved.Priority, message.Priority);

        using var repeated = await client.PutAsync($"/tasks/{created.Id}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        var unchanged = Assert.Single((await client.GetFromJsonAsync<TaskResponse[]>("/tasks", JsonOptions, TestContext.Current.CancellationToken))!,
            x => x.Id == created.Id);
        Assert.Equal(saved.CompletedAt, unchanged.CompletedAt);
        await factory.DisposeAsync();
        await probe.AssertNoMessageAsync();
    }

    [Fact(DisplayName = "Создание только с названием задаёт приоритет Medium и начальное состояние")]
    public async Task Create_Should_UseDefaults_When_OnlyTitleIsProvided()
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/tasks", new { title = new string('a', 200) }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = (await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
        Assert.Equal(Priority.Medium, task.Priority);
        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedAt);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.NotEqual(default, task.CreatedAt);
    }

    [Theory(DisplayName = "Пустое или пробельное название возвращает HTTP 400")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Create_Should_Return400_When_TitleIsBlank(string? title)
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/tasks", new { title }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Название длиннее 200 символов возвращает HTTP 400")]
    public async Task Create_Should_Return400_When_TitleIsTooLong()
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/tasks", new { title = new string('a', 201) }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory(DisplayName = "Неверный JSON или приоритет возвращает HTTP 400")]
    [InlineData("{\"title\":\"test\",\"priority\":99}")]
    [InlineData("{\"title\":\"test\",\"priority\":\"Urgent\"}")]
    [InlineData("{\"title\":\"test\",\"priority\":null}")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task Create_Should_Return400_When_BodyIsInvalid(string json)
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/tasks", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Конкурентное завершение одной версии строки возвращает один успех и один конфликт")]
    public async Task Complete_Should_ReturnOneConflict_When_BothRequestsReadTheSameRowVersion()
    {
        await using var probe = await RabbitMqProbe.CreateAsync(fixture.Broker);
        await using var factory = new ApiFactory(fixture.Configuration, new CompletionBarrier());
        using var client = factory.CreateClient();
        var task = await CreateAsync(client);

        var responses = await Task.WhenAll(
            client.PutAsync($"/tasks/{task.Id}/complete", null, TestContext.Current.CancellationToken),
            client.PutAsync($"/tasks/{task.Id}/complete", null, TestContext.Current.CancellationToken));

        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await probe.ReadAsync(task.Id);
        await factory.DisposeAsync();
        await probe.AssertNoMessageAsync();
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Недоступность RabbitMQ не мешает сохранению завершённой задачи")]
    public async Task Complete_Should_PersistTask_When_RabbitMqIsUnavailable()
    {
        await using var factory = new ApiFactory(fixture.Configuration, rabbitMqPort: 1);
        using var client = factory.CreateClient();
        var task = await CreateAsync(client);

        using var response = await client.PutAsync($"/tasks/{task.Id}/complete", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Tasks
            .AsNoTracking().SingleAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken);
        Assert.True(saved.IsCompleted);
        Assert.NotNull(saved.CompletedAt);
    }

    [Fact(DisplayName = "Ошибка сохранения возвращает HTTP 500 без изменения БД и без события")]
    public async Task Complete_Should_NotPublish_When_DatabaseSaveFails()
    {
        await using var probe = await RabbitMqProbe.CreateAsync(fixture.Broker);
        await using var factory = new ApiFactory(fixture.Configuration, new RejectCompletion());
        using var client = factory.CreateClient();
        var task = await CreateAsync(client);

        using var response = await client.PutAsync($"/tasks/{task.Id}/complete", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Tasks
            .AsNoTracking().SingleAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken);
        Assert.False(saved.IsCompleted);
        Assert.Null(saved.CompletedAt);
        await factory.DisposeAsync();
        await probe.AssertNoMessageAsync();
    }

    [Fact(DisplayName = "Удаление убирает задачу из БД, повторное обращение возвращает HTTP 404")]
    public async Task Delete_Should_RemoveTaskPermanently_When_TaskExists()
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        var task = await CreateAsync(client);

        using var response = await client.DeleteAsync($"/tasks/{task.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var tasks = (await client.GetFromJsonAsync<TaskResponse[]>("/tasks", JsonOptions, TestContext.Current.CancellationToken))!;
        Assert.DoesNotContain(tasks, x => x.Id == task.Id);
        using var missing = await client.DeleteAsync($"/tasks/{task.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var complete = await client.PutAsync($"/tasks/{task.Id}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
    }

    [Theory(DisplayName = "Некорректный ID возвращает HTTP 400, отсутствующая задача — HTTP 404")]
    [InlineData("not-a-guid", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.NotFound)]
    public async Task Endpoints_Should_ReturnExpectedStatus_When_IdIsInvalidOrMissing(
        string id, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(fixture.Configuration);
        using var client = factory.CreateClient();
        using var complete = await client.PutAsync($"/tasks/{id}/complete", null, TestContext.Current.CancellationToken);
        using var delete = await client.DeleteAsync($"/tasks/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(expected, complete.StatusCode);
        Assert.Equal(expected, delete.StatusCode);
    }

    private static async Task<TaskResponse> CreateAsync(HttpClient client, string title = "Test task", string priority = "Medium")
    {
        using var response = await client.PostAsJsonAsync("/tasks", new { title, priority }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    private sealed class RejectCompletion : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TaskItem>().Any(x => x.State == EntityState.Modified))
            {
                throw new InvalidOperationException("Simulated database write failure.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
