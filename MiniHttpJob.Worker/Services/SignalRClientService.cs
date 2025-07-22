namespace MiniHttpJob.Worker.Services;

public class SignalRClientService : ISignalRClientService, IDisposable
{
    private readonly ILogger<SignalRClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IJobQueueService _jobQueueService;
    private HubConnection? _connection;
    private Timer? _heartbeatTimer;
    private readonly WorkerInfo _workerInfo;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRClientService(
        ILogger<SignalRClientService> logger,
        IConfiguration configuration,
        IJobQueueService jobQueueService)
    {
        _logger = logger;
        _configuration = configuration;
        _jobQueueService = jobQueueService;

        // 初始化Worker信息
        _workerInfo = new WorkerInfo
        {
            InstanceName = _configuration.GetValue("Worker:InstanceName", Environment.MachineName),
            MachineName = Environment.MachineName,
            IpAddress = GetLocalIPAddress(),
            Port = _configuration.GetValue("Worker:Port", 5001),
            Capacity = new WorkerCapacity
            {
                MaxConcurrentJobs = _configuration.GetValue("Worker:MaxConcurrentJobs", 10)
            }
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var adminUrl = _configuration.GetValue("Admin:SignalRUrl", "https://localhost:5000/jobHub");

        _connection = new HubConnectionBuilder()
            .WithUrl($"{adminUrl}?clientType=worker")
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        // 设置接收信息的处理器
        _connection.On<JobExecutionCommand>("ExecuteJob", OnExecuteJob);
        _connection.On("HeartbeatCheck", OnHeartbeatCheck);

        // 设置连接状态变化事件
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += OnReconnected;
        _connection.Closed += OnClosed;

        try
        {
            await _connection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR connection established with Admin: {AdminUrl}", adminUrl);

            // 注册Worker
            await RegisterWorkerAsync();

            // 启动心跳定时器
            StartHeartbeatTimer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish SignalR connection with Admin: {AdminUrl}", adminUrl);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _heartbeatTimer?.Dispose();

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _logger.LogInformation("SignalR connection stopped");
    }

    public async Task RegisterWorkerAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("RegisterWorker", _workerInfo);
                _logger.LogInformation("Worker registered successfully: {InstanceName}", _workerInfo.InstanceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register worker");
                throw;
            }
        }
    }

    public async Task ReportJobCompletionAsync(JobExecutionResult result)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("ReportJobCompletion", result);
                _logger.LogDebug("Job completion reported: JobId={JobId}, Success={Success}", result.JobId, result.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report job completion for JobId={JobId}", result.JobId);
            }
        }
    }

    public async Task UpdateStatusAsync(WorkerStatusUpdate status)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("UpdateStatus", status);
                _logger.LogDebug("Status updated: {Status}", status.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update status");
            }
        }
    }

    public async Task SendHeartbeatAsync(WorkerHeartbeat heartbeat)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("Heartbeat", heartbeat);
                _logger.LogDebug("Heartbeat sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat");
            }
        }
    }

    private async Task OnExecuteJob(JobExecutionCommand command)
    {
        try
        {
            _logger.LogInformation("Received job execution command: JobId={JobId}, JobName={JobName}",
                command.JobId, command.JobName);

            // 将作业加入到执行队列
            await _jobQueueService.EnqueueJobAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle job execution command: JobId={JobId}", command.JobId);
        }
    }

    private async Task OnHeartbeatCheck()
    {
        try
        {
            var heartbeat = new WorkerHeartbeat
            {
                Timestamp = DateTime.UtcNow,
                Capacity = await GetCurrentCapacityAsync(),
                RunningJobIds = await _jobQueueService.GetRunningJobIdsAsync()
            };

            await SendHeartbeatAsync(heartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to respond to heartbeat check");
        }
    }

    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR connection lost, attempting to reconnect...");
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR connection reestablished");
        await RegisterWorkerAsync();
    }

    private Task OnClosed(Exception? exception)
    {
        _logger.LogError(exception, "SignalR connection closed");
        return Task.CompletedTask;
    }

    private void StartHeartbeatTimer()
    {
        _heartbeatTimer = new Timer(async _ =>
        {
            try
            {
                var heartbeat = new WorkerHeartbeat
                {
                    Timestamp = DateTime.UtcNow,
                    Capacity = await GetCurrentCapacityAsync(),
                    RunningJobIds = await _jobQueueService.GetRunningJobIdsAsync()
                };

                await SendHeartbeatAsync(heartbeat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send periodic heartbeat");
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private async Task<WorkerCapacity> GetCurrentCapacityAsync()
    {
        var currentJobs = await _jobQueueService.GetRunningJobCountAsync();
        var queueSize = await _jobQueueService.GetQueueSizeAsync();

        return new WorkerCapacity
        {
            MaxConcurrentJobs = _workerInfo.Capacity.MaxConcurrentJobs,
            CurrentRunningJobs = currentJobs,
            QueueSize = queueSize,
            CpuUsage = GetCpuUsage(),
            MemoryUsage = GetMemoryUsage()
        };
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            var localIP = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return localIP?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static double GetCpuUsage()
    {
        // 简化的CPU使用率获取，实际项目中可能使用更精确的方法
        return 0.0;
    }

    private static double GetMemoryUsage()
    {
        // 简化的内存使用率获取，实际项目中可能使用更精确的方法
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.WorkingSet64 / (1024.0 * 1024.0); // MB
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _connection?.DisposeAsync();
    }
}