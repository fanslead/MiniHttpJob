namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// SignalR Hub接口，定义Admin和Worker之间通讯方法
/// </summary>
public interface IJobHubClient
{
    /// <summary>
    /// 向Worker发送作业执行命令
    /// </summary>
    Task ExecuteJob(JobExecutionCommand command);
    
    /// <summary>
    /// 向Admin发送Worker状态更新
    /// </summary>
    Task UpdateWorkerStatus(WorkerStatusUpdate status);
    
    /// <summary>
    /// 向Admin发送作业执行结果
    /// </summary>
    Task JobExecutionCompleted(JobExecutionResult result);
    
    /// <summary>
    /// 向Admin发送Worker注册信息
    /// </summary>
    Task WorkerRegistered(WorkerInfo worker);
    
    /// <summary>
    /// 向Admin发送Worker断开连接信息
    /// </summary>
    Task WorkerDisconnected(string workerId);
    
    /// <summary>
    /// 向Worker发送心跳检查
    /// </summary>
    Task HeartbeatCheck();
    
    /// <summary>
    /// 向Admin发送心跳响应
    /// </summary>
    Task HeartbeatResponse(WorkerHeartbeat heartbeat);
}