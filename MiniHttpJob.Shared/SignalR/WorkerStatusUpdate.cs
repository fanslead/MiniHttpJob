namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// Worker×´Ì¬¸üÐÂ
/// </summary>
public class WorkerStatusUpdate
{
    public string WorkerId { get; set; } = null!;
    public WorkerStatus Status { get; set; }
    public WorkerCapacity Capacity { get; set; } = new();
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
}