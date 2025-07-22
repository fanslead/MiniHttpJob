namespace MiniHttpJob.Admin.Services;

/// <summary>
/// Worker心跳检查后台服务
/// </summary>
public class WorkerHeartbeatService : BackgroundService
{
    private readonly ILogger<WorkerHeartbeatService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<Hubs.JobHub, IJobHubClient> _hubContext;

    public WorkerHeartbeatService(
        ILogger<WorkerHeartbeatService> logger,
        IServiceProvider serviceProvider,
        IHubContext<Hubs.JobHub, IJobHubClient> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker heartbeat service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckWorkerHeartbeats();
                await SendHeartbeatChecks();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker heartbeat service");
            }

            // 每30秒检查一次
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Worker heartbeat service stopped");
    }

    private async Task CheckWorkerHeartbeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var workerManager = scope.ServiceProvider.GetRequiredService<IWorkerManager>();

        await workerManager.CleanupInactiveWorkersAsync();
    }

    private async Task SendHeartbeatChecks()
    {
        using var scope = _serviceProvider.CreateScope();
        var workerManager = scope.ServiceProvider.GetRequiredService<IWorkerManager>();

        var workers = await workerManager.GetAllWorkersAsync();

        if (workers.Any())
        {
            // 向所有Workers发送心跳检查
            await _hubContext.Clients.Group("Workers").HeartbeatCheck();
            _logger.LogDebug("Heartbeat check sent to {WorkerCount} workers", workers.Count());
        }
    }
}