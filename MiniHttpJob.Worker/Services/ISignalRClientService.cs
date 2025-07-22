namespace MiniHttpJob.Worker.Services;

/// <summary>
/// SignalR客户端服务，负责与Admin建立连接和通讯
/// </summary>
public interface ISignalRClientService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RegisterWorkerAsync();
    Task ReportJobCompletionAsync(JobExecutionResult result);
    Task UpdateStatusAsync(WorkerStatusUpdate status);
    Task SendHeartbeatAsync(WorkerHeartbeat heartbeat);
    bool IsConnected { get; }
}