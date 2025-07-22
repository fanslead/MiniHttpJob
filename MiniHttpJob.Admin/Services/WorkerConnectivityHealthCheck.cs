namespace MiniHttpJob.Admin.Services;

/// <summary>
/// 自定义健康检查：Worker连接状态
/// </summary>
public class WorkerConnectivityHealthCheck : IHealthCheck
{
    private readonly IWorkerManager _workerManager;
    private readonly ILogger<WorkerConnectivityHealthCheck> _logger;

    public WorkerConnectivityHealthCheck(
        IWorkerManager workerManager,
        ILogger<WorkerConnectivityHealthCheck> logger)
    {
        _workerManager = workerManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workers = await _workerManager.GetAllWorkersAsync();
            var availableWorkers = await _workerManager.GetAvailableWorkerInfoAsync();

            var totalWorkers = workers.Count();
            var availableWorkerCount = availableWorkers.Count();

            var data = new Dictionary<string, object>
            {
                ["total_workers"] = totalWorkers,
                ["available_workers"] = availableWorkerCount,
                ["healthy_percentage"] = totalWorkers > 0 ? (double)availableWorkerCount / totalWorkers * 100 : 0
            };

            if (totalWorkers == 0)
            {
                return HealthCheckResult.Degraded("No workers registered", data: data);
            }

            if (availableWorkerCount == 0)
            {
                return HealthCheckResult.Unhealthy("No available workers", data: data);
            }

            if ((double)availableWorkerCount / totalWorkers < 0.5)
            {
                return HealthCheckResult.Degraded($"Less than 50% workers available ({availableWorkerCount}/{totalWorkers})", data: data);
            }

            return HealthCheckResult.Healthy($"Workers healthy ({availableWorkerCount}/{totalWorkers})", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking worker connectivity health");
            return HealthCheckResult.Unhealthy("Failed to check worker connectivity", ex);
        }
    }
}