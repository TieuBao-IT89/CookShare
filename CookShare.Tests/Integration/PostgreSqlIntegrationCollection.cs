namespace CookShare.Tests.Integration
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class PostgreSqlIntegrationCollection
        : ICollectionFixture<CookShareWebApplicationFactory>
    {
        public const string Name = "PostgreSQL integration";
    }
}
