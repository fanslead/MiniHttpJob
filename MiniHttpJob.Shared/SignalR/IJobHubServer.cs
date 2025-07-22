namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// Hub����˽ӿڣ�����Worker���Ե��õķ���
/// </summary>
public interface IJobHubServer
{
    /// <summary>
    /// Workerע�ᵽAdmin
    /// </summary>
    Task RegisterWorker(WorkerInfo workerInfo);
    
    /// <summary>
    /// Worker������ҵִ�н��
    /// </summary>
    Task ReportJobCompletion(JobExecutionResult result);
    
    /// <summary>
    /// Worker����״̬
    /// </summary>
    Task UpdateStatus(WorkerStatusUpdate status);
    
    /// <summary>
    /// Worker��Ӧ����
    /// </summary>
    Task Heartbeat(WorkerHeartbeat heartbeat);
}