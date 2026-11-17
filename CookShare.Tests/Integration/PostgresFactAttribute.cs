namespace CookShare.Tests.Integration
{
    // Keep the PostgreSQL CRUD suite available, but require an explicit choice of
    // a disposable test database before its migration and write tests can run.
    public sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (!IsEnabled)
            {
                Skip = "Requires COOKSHARE_RUN_POSTGRES_TESTS=1 and a separate COOKSHARE_TEST_CONNECTION database.";
            }
        }

        public static bool IsEnabled => string.Equals(
                Environment.GetEnvironmentVariable("COOKSHARE_RUN_POSTGRES_TESTS"),
                "1",
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("COOKSHARE_TEST_CONNECTION"));
    }

    public sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute()
        {
            if (!PostgresFactAttribute.IsEnabled)
            {
                Skip = "Requires COOKSHARE_RUN_POSTGRES_TESTS=1 and a separate COOKSHARE_TEST_CONNECTION database.";
            }
        }
    }
}
