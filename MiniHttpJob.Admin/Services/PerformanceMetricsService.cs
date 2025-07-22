namespace MiniHttpJob.Admin.Services;

/// <summary>
/// 性能监控和指标收集服务
/// </summary>
public interface IPerformanceMetricsService
{
    void RecordJobDispatchTime(double milliseconds);
    void RecordJobDispatchSuccess();
    void RecordJobDispatchFailure(string reason);
    void RecordWorkerSelectionTime(double milliseconds);
    void RecordActiveJobs(int count);
    void RecordActiveWorkers(int count);
    Task<PerformanceMetrics> GetCurrentMetricsAsync();
}

public class PerformanceMetricsService : IPerformanceMetricsService, IDisposable
{
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly Meter _meter;

    // 计数器
    private readonly Counter<long> _jobDispatchSuccessCounter;
    private readonly Counter<long> _jobDispatchFailureCounter;

    // 直方图
    private readonly Histogram<double> _jobDispatchDuration;
    private readonly Histogram<double> _workerSelectionDuration;

    // 仪表
    private readonly ObservableGauge<int> _activeJobsGauge;
    private readonly ObservableGauge<int> _activeWorkersGauge;

    // 内部状态
    private volatile int _currentActiveJobs;
    private volatile int _currentActiveWorkers;
    private readonly Dictionary<string, long> _failureReasons = new();
    private readonly object _lock = new();

    public PerformanceMetricsService(ILogger<PerformanceMetricsService> logger)
    {
        _logger = logger;
        _meter = new Meter("MiniHttpJob.Admin", "1.0.0");

        // 初始化计数器
        _jobDispatchSuccessCounter = _meter.CreateCounter<long>(
            "job_dispatch_success_total",
            description: "Total number of successful job dispatches");

        _jobDispatchFailureCounter = _meter.CreateCounter<long>(
            "job_dispatch_failure_total",
            description: "Total number of failed job dispatches");

        // 初始化直方图
        _jobDispatchDuration = _meter.CreateHistogram<double>(
            "job_dispatch_duration_ms",
            unit: "ms",
            description: "Duration of job dispatch operations");

        _workerSelectionDuration = _meter.CreateHistogram<double>(
            "worker_selection_duration_ms",
            unit: "ms",
            description: "Duration of worker selection operations");

        // 初始化仪表
        _activeJobsGauge = _meter.CreateObservableGauge<int>(
            "active_jobs_current",
            () => _currentActiveJobs,
            description: "Current number of active jobs");

        _activeWorkersGauge = _meter.CreateObservableGauge<int>(
            "active_workers_current",
            () => _currentActiveWorkers,
            description: "Current number of active workers");
    }

    public void RecordJobDispatchTime(double milliseconds)
    {
        _jobDispatchDuration.Record(milliseconds);
    }

    public void RecordJobDispatchSuccess()
    {
        _jobDispatchSuccessCounter.Add(1);
    }

    public void RecordJobDispatchFailure(string reason)
    {
        _jobDispatchFailureCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));

        lock (_lock)
        {
            _failureReasons[reason] = _failureReasons.GetValueOrDefault(reason, 0) + 1;
        }
    }

    public void RecordWorkerSelectionTime(double milliseconds)
    {
        _workerSelectionDuration.Record(milliseconds);
    }

    public void RecordActiveJobs(int count)
    {
        _currentActiveJobs = count;
    }

    public void RecordActiveWorkers(int count)
    {
        _currentActiveWorkers = count;
    }

    public Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        lock (_lock)
        {
            var metrics = new PerformanceMetrics
            {
                ActiveJobs = _currentActiveJobs,
                ActiveWorkers = _currentActiveWorkers,
                FailureReasons = new Dictionary<string, long>(_failureReasons),
                Timestamp = DateTime.UtcNow
            };

            return Task.FromResult(metrics);
        }
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// 性能指标数据模型
/// </summary>
public class PerformanceMetrics
{
    public int ActiveJobs { get; set; }
    public int ActiveWorkers { get; set; }
    public Dictionary<string, long> FailureReasons { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 分布式追踪服务
/// </summary>
public interface IDistributedTracingService
{
    Activity? StartActivity(string name);
    void AddEvent(Activity? activity, string name, object? data = null);
    void SetStatus(Activity? activity, bool success, string? description = null);
}

public class DistributedTracingService : IDistributedTracingService
{
    private readonly ActivitySource _activitySource;
    private readonly ILogger<DistributedTracingService> _logger;

    public DistributedTracingService(ILogger<DistributedTracingService> logger)
    {
        _logger = logger;
        _activitySource = new ActivitySource("MiniHttpJob.DistributedTracing", "1.0.0");
    }

    public Activity? StartActivity(string name)
    {
        var activity = _activitySource.StartActivity(name);
        if (activity != null)
        {
            activity.SetTag("service.name", "MiniHttpJob.Admin");
            activity.SetTag("service.version", "1.0.0");
        }
        return activity;
    }

    public void AddEvent(Activity? activity, string name, object? data = null)
    {
        if (activity != null)
        {
            var tags = data != null
                ? new Dictionary<string, object?> { ["data"] = JsonSerializer.Serialize(data) }
                : new Dictionary<string, object?>();

            activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, new ActivityTagsCollection(tags)));
        }
    }

    public void SetStatus(Activity? activity, bool success, string? description = null)
    {
        if (activity != null)
        {
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error, description);
        }
    }
}