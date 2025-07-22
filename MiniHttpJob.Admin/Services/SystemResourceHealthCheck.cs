namespace MiniHttpJob.Admin.Services;

/// <summary>
/// �Զ��彡����飺ϵͳ��Դʹ�����
/// </summary>
public class SystemResourceHealthCheck : IHealthCheck
{
    private readonly ILogger<SystemResourceHealthCheck> _logger;

    public SystemResourceHealthCheck(ILogger<SystemResourceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // �ڴ�ʹ�ü��
            var memoryUsed = GC.GetTotalMemory(false);
            var memoryUsedMB = memoryUsed / 1024 / 1024;

            // �̳߳�״̬���
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            var workerThreadUsage = (double)(maxWorkerThreads - availableWorkerThreads) / maxWorkerThreads;
            var completionPortUsage = (double)(maxCompletionPortThreads - availableCompletionPortThreads) / maxCompletionPortThreads;

            var data = new Dictionary<string, object>
            {
                ["memory_used_mb"] = memoryUsedMB,
                ["worker_thread_usage_percentage"] = Math.Round(workerThreadUsage * 100, 2),
                ["completion_port_usage_percentage"] = Math.Round(completionPortUsage * 100, 2),
                ["available_worker_threads"] = availableWorkerThreads,
                ["available_completion_port_threads"] = availableCompletionPortThreads
            };

            // ����ڴ�ʹ�ã���ֵ��1GB
            if (memoryUsedMB > 1024)
            {
                return Task.FromResult(HealthCheckResult.Degraded($"High memory usage: {memoryUsedMB}MB", data: data));
            }

            // ����̳߳�ʹ���ʣ���ֵ��80%
            if (workerThreadUsage > 0.8 || completionPortUsage > 0.8)
            {
                return Task.FromResult(HealthCheckResult.Degraded("High thread pool usage", data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy("System resources healthy", data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system resource health");
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to check system resources", ex));
        }
    }
}