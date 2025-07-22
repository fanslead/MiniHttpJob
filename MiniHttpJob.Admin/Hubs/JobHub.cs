namespace MiniHttpJob.Admin.Hubs;

/// <summary>
/// 作业调度Hub，负责Admin和Worker之间的实时通信
/// </summary>
public class JobHub : Hub<IJobHubClient>, IJobHubServer
{
    private readonly ILogger<JobHub> _logger;
    private readonly IWorkerManager _workerManager;

    public JobHub(ILogger<JobHub> logger, IWorkerManager workerManager)
    {
        _logger = logger;
        _workerManager = workerManager;
    }

    /// <summary>
    /// Worker注册到Admin
    /// </summary>
    public async Task RegisterWorker(WorkerInfo workerInfo)
    {
        try
        {
            workerInfo.WorkerId = Context.ConnectionId;
            await _workerManager.RegisterWorkerAsync(workerInfo);

            // 将Worker加入到Workers组
            await Groups.AddToGroupAsync(Context.ConnectionId, "Workers");

            // 通知所有Admin客户端有新Worker注册
            await Clients.Group("Admins").WorkerRegistered(workerInfo);

            _logger.LogInformation("Worker {WorkerId} registered: {InstanceName}",
                workerInfo.WorkerId, workerInfo.InstanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register worker {WorkerId}", Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Worker报告作业执行完成
    /// </summary>
    public async Task ReportJobCompletion(JobExecutionResult result)
    {
        try
        {
            result.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateJobExecutionResultAsync(result);

            // 通知Admin作业执行完成
            await Clients.Group("Admins").JobExecutionCompleted(result);

            _logger.LogInformation("Job {JobId} completed by worker {WorkerId} with status {Success}",
                result.JobId, result.WorkerId, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job completion report from worker {WorkerId}", Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Worker更新状态
    /// </summary>
    public async Task UpdateStatus(WorkerStatusUpdate status)
    {
        try
        {
            status.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateWorkerStatusAsync(status);

            // 通知Admin Worker状态更新
            await Clients.Group("Admins").UpdateWorkerStatus(status);

            _logger.LogDebug("Worker {WorkerId} status updated: {Status}", status.WorkerId, status.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worker status for {WorkerId}", Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Worker响应心跳
    /// </summary>
    public async Task Heartbeat(WorkerHeartbeat heartbeat)
    {
        try
        {
            heartbeat.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateWorkerHeartbeatAsync(heartbeat);

            // 通知Admin心跳响应
            await Clients.Group("Admins").HeartbeatResponse(heartbeat);

            _logger.LogDebug("Heartbeat received from worker {WorkerId}", heartbeat.WorkerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat from worker {WorkerId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// 客户端连接时
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var clientType = httpContext?.Request.Query["clientType"].ToString();

        if (clientType == "admin")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            _logger.LogInformation("Admin client connected: {ConnectionId}", Context.ConnectionId);
        }
        else if (clientType == "worker")
        {
            _logger.LogInformation("Worker client connected: {ConnectionId}", Context.ConnectionId);
            // Worker需要通过RegisterWorker方法正式注册
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;

            // 如果是Worker断开连接，需要清理Worker信息
            var worker = await _workerManager.GetWorkerByIdAsync(connectionId);
            if (worker != null)
            {
                await _workerManager.UnregisterWorkerAsync(connectionId);
                await Clients.Group("Admins").WorkerDisconnected(connectionId);

                _logger.LogInformation("Worker {WorkerId} disconnected: {InstanceName}",
                    connectionId, worker.InstanceName);
            }
            else
            {
                _logger.LogInformation("Admin client disconnected: {ConnectionId}", connectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client disconnection for {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}