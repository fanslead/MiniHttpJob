namespace MiniHttpJob.Admin.Configuration;

public class JobSchedulerOptions
{
    public const string SectionName = "JobScheduler";

    public bool EnableClustering { get; set; } = true;
    public string InstanceName { get; set; } = "MiniHttpJobScheduler";
    public int MaxConcurrentJobs { get; set; } = 10;
    public int MisfireThreshold { get; set; } = 60000; // 1 minute
    
    // 分布式作业执行设置
    public int JobFetchTimeoutSeconds { get; set; } = 5;
    public int WorkerSelectionTimeoutSeconds { get; set; } = 5;
    public int JobDispatchTimeoutSeconds { get; set; } = 30;
    public int MaxWorkerRetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int DefaultJobTimeoutSeconds { get; set; } = 30;
    
    // 性能和监控设置
    public bool EnablePerformanceMetrics { get; set; } = true;
    public bool EnableDistributedTracing { get; set; } = false;
    public string MetricsEndpoint { get; set; } = "/metrics";
}