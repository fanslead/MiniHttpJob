namespace MiniHttpJob.Admin.Hubs;

/// <summary>
/// ��ҵ����Hub������Admin��Worker֮���ʵʱͨ��
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
    /// Workerע�ᵽAdmin
    /// </summary>
    public async Task RegisterWorker(WorkerInfo workerInfo)
    {
        try
        {
            workerInfo.WorkerId = Context.ConnectionId;
            await _workerManager.RegisterWorkerAsync(workerInfo);

            // ��Worker���뵽Workers��
            await Groups.AddToGroupAsync(Context.ConnectionId, "Workers");

            // ֪ͨ����Admin�ͻ�������Workerע��
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
    /// Worker������ҵִ�����
    /// </summary>
    public async Task ReportJobCompletion(JobExecutionResult result)
    {
        try
        {
            result.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateJobExecutionResultAsync(result);

            // ֪ͨAdmin��ҵִ�����
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
    /// Worker����״̬
    /// </summary>
    public async Task UpdateStatus(WorkerStatusUpdate status)
    {
        try
        {
            status.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateWorkerStatusAsync(status);

            // ֪ͨAdmin Worker״̬����
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
    /// Worker��Ӧ����
    /// </summary>
    public async Task Heartbeat(WorkerHeartbeat heartbeat)
    {
        try
        {
            heartbeat.WorkerId = Context.ConnectionId;
            await _workerManager.UpdateWorkerHeartbeatAsync(heartbeat);

            // ֪ͨAdmin������Ӧ
            await Clients.Group("Admins").HeartbeatResponse(heartbeat);

            _logger.LogDebug("Heartbeat received from worker {WorkerId}", heartbeat.WorkerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat from worker {WorkerId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// �ͻ�������ʱ
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
            // Worker��Ҫͨ��RegisterWorker������ʽע��
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// �ͻ��˶Ͽ�����ʱ
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;

            // �����Worker�Ͽ����ӣ���Ҫ����Worker��Ϣ
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