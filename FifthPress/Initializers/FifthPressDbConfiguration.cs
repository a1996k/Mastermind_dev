using System;
using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace FifthPress.Initializers
{
    /// <summary>
    /// Configures Entity Framework to automatically retry failed database operations
    /// due to transient errors like deadlocks, timeouts, and connection failures.
    /// </summary>
    public class FifthPressDbConfiguration : DbConfiguration
    {
        public FifthPressDbConfiguration()
        {
            SetExecutionStrategy(
                "System.Data.SqlClient",
                () => new SqlAzureExecutionStrategy(
                    maxRetryCount: 3,
                    maxDelay: TimeSpan.FromSeconds(5)
                )
            );
        }
    }
}
