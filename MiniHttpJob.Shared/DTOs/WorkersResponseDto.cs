namespace MiniHttpJob.Shared.DTOs;

public class WorkersResponseDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public List<WorkerDetailDto> Workers { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class WorkerDetailDto
{
    public string Id { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
    public string MachineName { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public int Port { get; set; }
    public DateTime RegisterTime { get; set; }
    public bool IsAvailable { get; set; }
    public WorkerCapacityDto Capacity { get; set; } = null!;
    public string Version { get; set; } = null!;
}