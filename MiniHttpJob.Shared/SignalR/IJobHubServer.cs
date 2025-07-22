namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// Hub服务端接口，定义Worker可以调用的方法
/// </summary>
public interface IJobHubServer
{
    /// <summary>
    /// Worker注册到Admin
    /// </summary>
    Task RegisterWorker(WorkerInfo workerInfo);
    
    /// <summary>
    /// Worker报告作业执行结果
    /// </summary>
    Task ReportJobCompletion(JobExecutionResult result);
    
    /// <summary>
    /// Worker更新状态
    /// </summary>
    Task UpdateStatus(WorkerStatusUpdate status);
    
    /// <summary>
    /// Worker响应心跳
    /// </summary>
    Task Heartbeat(WorkerHeartbeat heartbeat);
}