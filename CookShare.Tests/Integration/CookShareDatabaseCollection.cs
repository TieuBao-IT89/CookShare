namespace CookShare.Tests.Integration
{
    [CollectionDefinition(Name)]
    public sealed class CookShareDatabaseCollection
        : ICollectionFixture<CookShareWebApplicationFactory>
    {
        public const string Name = "CookShare database";
    }
}
