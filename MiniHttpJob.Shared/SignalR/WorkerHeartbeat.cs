namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// WorkerĞÄÌøĞÅÏ¢
/// </summary>
public class WorkerHeartbeat
{
    public string WorkerId { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public WorkerCapacity Capacity { get; set; } = new();
    public List<int> RunningJobIds { get; set; } = new();
}