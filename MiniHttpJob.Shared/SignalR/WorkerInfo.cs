namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// Worker–≈œ¢
/// </summary>
public class WorkerInfo
{
    public string WorkerId { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
    public string MachineName { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public int Port { get; set; }
    public DateTime RegisterTime { get; set; } = DateTime.UtcNow;
    public WorkerCapacity Capacity { get; set; } = new();
    public string Version { get; set; } = "1.0.0";
}