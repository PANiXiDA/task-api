namespace PANiXiDA.TaskApi.Tasks.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollectionDefinition : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "Интеграционные тесты задач";
}
