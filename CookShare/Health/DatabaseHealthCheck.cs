using CookShare.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CookShare.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _dbContext;

        public DatabaseHealthCheck(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _dbContext.Database
                    .CanConnectAsync(cancellationToken);

                if (canConnect)
                {
                    return HealthCheckResult.Healthy();
                }
            }
            catch (Exception)
            {
                // Không đưa exception vào kết quả: thông báo của driver
                // có thể chứa máy chủ hoặc thông tin kết nối.
            }

            return HealthCheckResult.Unhealthy(
                "Database connection failed.");
        }
    }
}
