namespace MiniHttpJob.Admin.Controllers;

/// <summary>
/// 监控和指标API控制器
/// </summary>
[MiniController("api/[controller]")]
public class MonitoringController
{
    private readonly ILogger<MonitoringController> _logger;
    private readonly IPerformanceMetricsService _metricsService;
    private readonly HealthCheckService _healthCheckService;
    private readonly IWorkerManager _workerManager;
    private readonly IServiceProvider _serviceProvider;

    public MonitoringController(
        ILogger<MonitoringController> logger,
        IPerformanceMetricsService metricsService,
        HealthCheckService healthCheckService,
        IWorkerManager workerManager,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _metricsService = metricsService;
        _healthCheckService = healthCheckService;
        _workerManager = workerManager;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 获取系统健康状态
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthCheckDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<Results<Ok<HealthCheckDto>, StatusCodeHttpResult>> GetHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();

            var response = new HealthCheckDto
            {
                Status = healthReport.Status.ToString(),
                TotalDuration = healthReport.TotalDuration.TotalMilliseconds,
                Checks = healthReport.Entries.Select(kvp => new HealthCheckEntryDto
                {
                    Name = kvp.Key,
                    Status = kvp.Value.Status.ToString(),
                    Description = kvp.Value.Description,
                    Duration = kvp.Value.Duration.TotalMilliseconds,
                    Data = kvp.Value.Data,
                    Exception = kvp.Value.Exception?.Message
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            if (healthReport.Status == HealthStatus.Healthy)
            {
                return TypedResults.Ok(response);
            }
            else
            {
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status");
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// 获取性能指标
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(MetricsDto), StatusCodes.Status200OK)]
    public async Task<Results<Ok<MetricsDto>, ProblemHttpResult>> GetMetrics()
    {
        try
        {
            var metrics = await _metricsService.GetCurrentMetricsAsync();
            var workers = await _workerManager.GetAllWorkerInfoAsync();
            var availableWorkers = await _workerManager.GetAvailableWorkerInfoAsync();

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            var totalJobs = await dbContext.Jobs.CountAsync();
            var activeJobs = await dbContext.Jobs.CountAsync(j => j.Status == "Active");

            // 最近24小时的执行统计
            var since24h = DateTime.UtcNow.AddHours(-24);
            var recent24hExecutions = await dbContext.JobExecutions
                .Where(e => e.ExecutionTime >= since24h)
                .ToListAsync();

            var response = new MetricsDto
            {
                System = new SystemMetricsDto
                {
                    ActiveJobs = metrics.ActiveJobs,
                    ActiveWorkers = metrics.ActiveWorkers,
                    TotalJobs = totalJobs,
                    TotalWorkers = workers.Count(),
                    AvailableWorkers = availableWorkers.Count(),
                    Timestamp = metrics.Timestamp
                },
                ExecutionStats24h = new ExecutionStatsDto
                {
                    Total = recent24hExecutions.Count,
                    Successful = recent24hExecutions.Count(e => e.Status == "Success"),
                    Failed = recent24hExecutions.Count(e => e.Status == "Failed"),
                    SuccessRate = recent24hExecutions.Count > 0
                        ? Math.Round((double)recent24hExecutions.Count(e => e.Status == "Success") / recent24hExecutions.Count * 100, 2)
                        : 0
                },
                FailureReasons = metrics.FailureReasons,
                Workers = workers.Select(w => new WorkerDto
                {
                    Id = w.WorkerId,
                    InstanceName = w.InstanceName,
                    MachineName = w.MachineName,
                    IpAddress = w.IpAddress,
                    Port = w.Port,
                    RegisterTime = w.RegisterTime,
                    Capacity = new WorkerCapacityDto
                    {
                        MaxConcurrentJobs = w.Capacity.MaxConcurrentJobs,
                        CurrentRunningJobs = w.Capacity.CurrentRunningJobs,
                        QueueSize = w.Capacity.QueueSize,
                        CpuUsage = w.Capacity.CpuUsage,
                        MemoryUsage = w.Capacity.MemoryUsage
                    },
                    Version = w.Version
                }).ToList()
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return TypedResults.Problem("Failed to retrieve metrics", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 获取系统统计信息
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(StatisticsResponseDto), StatusCodes.Status200OK)]
    public async Task<Results<Ok<StatisticsResponseDto>, BadRequest<ErrorResponseDto>, ProblemHttpResult>> GetStatistics([FromQuery] int hours = 24)
    {
        try
        {
            if (hours <= 0 || hours > 168) // 最多7天
            {
                var errorResponse = new ErrorResponseDto { Error = "Hours parameter must be between 1 and 168" };
                return TypedResults.BadRequest(errorResponse);
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            var since = DateTime.UtcNow.AddHours(-hours);
            var executions = await dbContext.JobExecutions
                .Where(e => e.ExecutionTime >= since)
                .Include(e => e.Job)
                .ToListAsync();

            // 按小时分组统计
            var executionsByHour = executions
                .GroupBy(e => e.ExecutionTime.ToString("yyyy-MM-dd HH:00"))
                .ToDictionary(
                    g => g.Key,
                    g => new HourlyExecutionDto 
                    { 
                        Total = g.Count(), 
                        Successful = g.Count(e => e.Status == "Success") 
                    }
                );

            // 按作业分组统计
            var executionsByJob = executions
                .GroupBy(e => new { e.JobId, JobName = e.Job?.Name ?? "Unknown" })
                .Select(g => new JobExecutionStatsDto
                {
                    JobId = g.Key.JobId,
                    JobName = g.Key.JobName,
                    Total = g.Count(),
                    Successful = g.Count(e => e.Status == "Success"),
                    Failed = g.Count(e => e.Status == "Failed"),
                    SuccessRate = g.Count() > 0 ? Math.Round((double)g.Count(e => e.Status == "Success") / g.Count() * 100, 2) : 0,
                    AvgDuration = g.Where(e => !string.IsNullOrEmpty(e.Response) && e.Response.Contains("Duration"))
                        .Select(e => ExtractDurationFromResponse(e.Response))
                        .Where(d => d > 0)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var response = new StatisticsResponseDto
            {
                Period = new PeriodDto
                {
                    Hours = hours,
                    From = since,
                    To = DateTime.UtcNow
                },
                Summary = new ExecutionSummaryDto
                {
                    TotalExecutions = executions.Count,
                    SuccessfulExecutions = executions.Count(e => e.Status == "Success"),
                    FailedExecutions = executions.Count(e => e.Status == "Failed"),
                    SuccessRate = executions.Count > 0
                        ? Math.Round((double)executions.Count(e => e.Status == "Success") / executions.Count * 100, 2)
                        : 0
                },
                ExecutionsByHour = executionsByHour,
                ExecutionsByJob = executionsByJob.Take(20).ToList(), // 前20个最活跃的作业
                TopFailingJobs = executionsByJob
                    .Where(j => j.Failed > 0)
                    .OrderByDescending(j => j.Failed)
                    .Take(10)
                    .ToList()
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for {Hours} hours", hours);
            return TypedResults.Problem("Failed to retrieve statistics", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 获取Worker详细信息
    /// </summary>
    [HttpGet("workers")]
    [ProducesResponseType(typeof(WorkersResponseDto), StatusCodes.Status200OK)]
    public async Task<Results<Ok<WorkersResponseDto>, ProblemHttpResult>> GetWorkers()
    {
        try
        {
            var workers = await _workerManager.GetAllWorkerInfoAsync();
            var availableWorkers = await _workerManager.GetAvailableWorkerInfoAsync();
            var availableWorkerIds = availableWorkers.Select(w => w.WorkerId).ToHashSet();

            var response = new WorkersResponseDto
            {
                Total = workers.Count(),
                Available = availableWorkers.Count(),
                Workers = workers.Select(w => new WorkerDetailDto
                {
                    Id = w.WorkerId,
                    InstanceName = w.InstanceName,
                    MachineName = w.MachineName,
                    IpAddress = w.IpAddress,
                    Port = w.Port,
                    RegisterTime = w.RegisterTime,
                    IsAvailable = availableWorkerIds.Contains(w.WorkerId),
                    Capacity = new WorkerCapacityDto
                    {
                        MaxConcurrentJobs = w.Capacity.MaxConcurrentJobs,
                        CurrentRunningJobs = w.Capacity.CurrentRunningJobs,
                        QueueSize = w.Capacity.QueueSize,
                        CpuUsage = w.Capacity.CpuUsage,
                        MemoryUsage = w.Capacity.MemoryUsage,
                        UtilizationPercentage = w.Capacity.MaxConcurrentJobs > 0
                            ? Math.Round((double)w.Capacity.CurrentRunningJobs / w.Capacity.MaxConcurrentJobs * 100, 2)
                            : 0
                    },
                    Version = w.Version
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving worker information");
            return TypedResults.Problem("Failed to retrieve worker information", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 从响应字符串中获取执行时长（毫秒）
    /// </summary>
    private static double ExtractDurationFromResponse(string response)
    {
        try
        {
            if (string.IsNullOrEmpty(response) || !response.Contains("Duration:"))
                return 0;

            var durationPart = response.Split("Duration:")[1].Split("ms")[0].Trim();
            return double.TryParse(durationPart, out var duration) ? duration : 0;
        }
        catch
        {
            return 0;
        }
    }
}