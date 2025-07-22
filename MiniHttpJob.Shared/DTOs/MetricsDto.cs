namespace MiniHttpJob.Shared.DTOs;

public class MetricsDto
{
    public SystemMetricsDto System { get; set; } = null!;
    public ExecutionStatsDto ExecutionStats24h { get; set; } = null!;
    public Dictionary<string, long> FailureReasons { get; set; } = new();
    public List<WorkerDto> Workers { get; set; } = new();
}

public class SystemMetricsDto
{
    public int ActiveJobs { get; set; }
    public int ActiveWorkers { get; set; }
    public int TotalJobs { get; set; }
    public int TotalWorkers { get; set; }
    public int AvailableWorkers { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ExecutionStatsDto
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double SuccessRate { get; set; }
}

public class WorkerDto
{
    public string Id { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
    public string MachineName { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public int Port { get; set; }
    public DateTime RegisterTime { get; set; }
    public WorkerCapacityDto? Capacity { get; set; }
    public string Version { get; set; } = null!;
}

public class WorkerCapacityDto
{
    public int MaxConcurrentJobs { get; set; }
    public int CurrentRunningJobs { get; set; }
    public int QueueSize { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double UtilizationPercentage { get; set; }
}