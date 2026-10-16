namespace PANiXiDA.TaskApi.Tasks.IntegrationTests;

[Collection(IntegrationTestCollectionDefinition.Name)]
public abstract class IntegrationTestBase(IntegrationTestFixture fixture) : IAsyncLifetime
{
    protected IntegrationTestFixture Fixture { get; } = fixture;

    public async ValueTask InitializeAsync() => await Fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
