namespace MiniHttpJob.Admin.Services;

/// <summary>
/// 自定义健康检查：作业执行状态
/// </summary>
public class JobExecutionHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutionHealthCheck> _logger;

    public JobExecutionHealthCheck(
        IServiceProvider serviceProvider,
        ILogger<JobExecutionHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            // 检查最近5分钟的作业执行情况
            var since = DateTime.UtcNow.AddMinutes(-5);
            var recentExecutions = await dbContext.JobExecutions
                .Where(e => e.ExecutionTime >= since)
                .ToListAsync(cancellationToken);

            var totalExecutions = recentExecutions.Count;
            var failedExecutions = recentExecutions.Count(e => e.Status == "Failed");
            var failureRate = totalExecutions > 0 ? (double)failedExecutions / totalExecutions : 0;

            var data = new Dictionary<string, object>
            {
                ["total_executions_5min"] = totalExecutions,
                ["failed_executions_5min"] = failedExecutions,
                ["failure_rate_percentage"] = Math.Round(failureRate * 100, 2),
                ["check_period"] = "5 minutes"
            };

            if (totalExecutions == 0)
            {
                return HealthCheckResult.Healthy("No recent executions to evaluate", data: data);
            }

            if (failureRate > 0.5) // 50%以上失败率
            {
                return HealthCheckResult.Unhealthy($"High failure rate: {failureRate:P1}", data: data);
            }

            if (failureRate > 0.2) // 20%以上失败率
            {
                return HealthCheckResult.Degraded($"Elevated failure rate: {failureRate:P1}", data: data);
            }

            return HealthCheckResult.Healthy($"Execution health good: {failureRate:P1} failure rate", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking job execution health");
            return HealthCheckResult.Unhealthy("Failed to check job execution health", ex);
        }
    }
}