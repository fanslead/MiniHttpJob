namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// SignalR Hub�ӿڣ�����Admin��Worker֮��ͨѶ����
/// </summary>
public interface IJobHubClient
{
    /// <summary>
    /// ��Worker������ҵִ������
    /// </summary>
    Task ExecuteJob(JobExecutionCommand command);
    
    /// <summary>
    /// ��Admin����Worker״̬����
    /// </summary>
    Task UpdateWorkerStatus(WorkerStatusUpdate status);
    
    /// <summary>
    /// ��Admin������ҵִ�н��
    /// </summary>
    Task JobExecutionCompleted(JobExecutionResult result);
    
    /// <summary>
    /// ��Admin����Workerע����Ϣ
    /// </summary>
    Task WorkerRegistered(WorkerInfo worker);
    
    /// <summary>
    /// ��Admin����Worker�Ͽ�������Ϣ
    /// </summary>
    Task WorkerDisconnected(string workerId);
    
    /// <summary>
    /// ��Worker�����������
    /// </summary>
    Task HeartbeatCheck();
    
    /// <summary>
    /// ��Admin����������Ӧ
    /// </summary>
    Task HeartbeatResponse(WorkerHeartbeat heartbeat);
}