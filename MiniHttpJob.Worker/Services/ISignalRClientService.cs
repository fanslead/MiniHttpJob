namespace MiniHttpJob.Worker.Services;

/// <summary>
/// SignalR�ͻ��˷��񣬸�����Admin�������Ӻ�ͨѶ
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