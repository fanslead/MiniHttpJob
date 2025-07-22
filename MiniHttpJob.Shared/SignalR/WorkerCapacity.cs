namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// Worker»›¡ø–≈œ¢
/// </summary>
public class WorkerCapacity
{
    public int MaxConcurrentJobs { get; set; } = 10;
    public int CurrentRunningJobs { get; set; } = 0;
    public int QueueSize { get; set; } = 0;
    public double CpuUsage { get; set; } = 0;
    public double MemoryUsage { get; set; } = 0;
}