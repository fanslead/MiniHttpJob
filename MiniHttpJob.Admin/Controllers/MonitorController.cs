namespace MiniHttpJob.Admin.Controllers;

[MiniController("api/[controller]")]
public class MonitorController
{
    private readonly JobDbContext _dbContext;
    private readonly ILogger<MonitorController> _logger;

    public MonitorController(JobDbContext dbContext, ILogger<MonitorController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<DashboardResponseDto>, ProblemHttpResult>> GetDashboard()
    {
        try
        {
            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var lastHour = now.AddHours(-1);

            var totalJobs = await _dbContext.Jobs.CountAsync();
            var activeJobs = await _dbContext.Jobs.CountAsync(j => j.Status == "Active");
            var pausedJobs = await _dbContext.Jobs.CountAsync(j => j.Status == "Paused");

            var executionsLast24Hours = await _dbContext.JobExecutions
                .CountAsync(e => e.ExecutionTime >= last24Hours);

            var executionsLastHour = await _dbContext.JobExecutions
                .CountAsync(e => e.ExecutionTime >= lastHour);

            var recentExecutions = await _dbContext.JobExecutions
                .Include(e => e.Job)
                .OrderByDescending(e => e.ExecutionTime)
                .Take(10)
                .Select(e => new RecentExecutionDto
                {
                    Id = e.Id,
                    JobId = e.JobId,
                    JobName = e.Job != null ? e.Job.Name : "Unknown",
                    ExecutionTime = e.ExecutionTime,
                    Status = e.Status,
                    ErrorMessage = e.ErrorMessage
                })
                .ToListAsync();

            var successRate = await CalculateSuccessRateAsync(last24Hours);
            var avgExecutionsPerHour = await CalculateAverageExecutionsPerHour(last24Hours);

            var response = new DashboardResponseDto
            {
                Summary = new DashboardSummaryDto
                {
                    TotalJobs = totalJobs,
                    ActiveJobs = activeJobs,
                    PausedJobs = pausedJobs,
                    SuccessRate = Math.Round(successRate, 2),
                    ExecutionsLast24Hours = executionsLast24Hours,
                    ExecutionsLastHour = executionsLastHour,
                    AverageExecutionsPerHour = Math.Round(avgExecutionsPerHour, 2)
                },
                RecentExecutions = recentExecutions,
                SystemInfo = new SystemInfoDto
                {
                    ServerTime = now,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = GC.GetTotalMemory(false),
                    Uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime())
                }
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard data");
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(MonitorStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<MonitorStatisticsDto>, ProblemHttpResult>> GetStatistics()
    {
        try
        {
            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            var response = new MonitorStatisticsDto
            {
                ExecutionStats = new ExecutionStatsGroupDto
                {
                    Last24Hours = await GetExecutionStatsDto(last24Hours),
                    Last7Days = await GetExecutionStatsDto(last7Days),
                    Last30Days = await GetExecutionStatsDto(last30Days)
                },
                JobsByStatus = await _dbContext.Jobs
                    .GroupBy(j => j.Status)
                    .Select(g => new JobStatusCountDto { Status = g.Key, Count = g.Count() })
                    .ToListAsync(),
                TopFailingJobs = await GetTopFailingJobsDto(last7Days),
                HourlyExecutionTrend = await GetHourlyExecutionTrendDto(last24Hours)
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("jobs/{id}/performance")]
    [ProducesResponseType(typeof(JobPerformanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<JobPerformanceDto>, NotFound, ProblemHttpResult>> GetJobPerformance(int id)
    {
        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null)
                return TypedResults.NotFound();

            var last30Days = DateTime.UtcNow.AddDays(-30);
            var executions = await _dbContext.JobExecutions
                .Where(e => e.JobId == id && e.ExecutionTime >= last30Days)
                .OrderBy(e => e.ExecutionTime)
                .ToListAsync();

            var response = new JobPerformanceDto
            {
                JobInfo = new JobInfoDto
                {
                    Id = job.Id,
                    Name = job.Name,
                    Status = job.Status,
                    CronExpression = job.CronExpression,
                    Url = job.Url
                },
                ExecutionSummary = new JobExecutionSummaryDto
                {
                    TotalExecutions = executions.Count,
                    SuccessfulExecutions = executions.Count(e => e.Status == "Success"),
                    FailedExecutions = executions.Count(e => e.Status == "Failed"),
                    SuccessRate = executions.Count > 0 ?
                        Math.Round((double)executions.Count(e => e.Status == "Success") / executions.Count * 100, 2) : 0,
                    LastExecution = executions.LastOrDefault()?.ExecutionTime,
                    NextExecution = GetNextExecutionTime(job.CronExpression)
                },
                DailyTrend = executions
                    .GroupBy(e => e.ExecutionTime.Date)
                    .Select(g => new DailyTrendDto
                    {
                        Date = g.Key,
                        TotalExecutions = g.Count(),
                        SuccessfulExecutions = g.Count(e => e.Status == "Success"),
                        FailedExecutions = g.Count(e => e.Status == "Failed")
                    })
                    .OrderBy(x => x.Date)
                    .ToList(),
                RecentFailures = executions
                    .Where(e => e.Status == "Failed")
                    .OrderByDescending(e => e.ExecutionTime)
                    .Take(10)
                    .Select(e => new RecentFailureDto
                    {
                        ExecutionTime = e.ExecutionTime,
                        ErrorMessage = e.ErrorMessage
                    })
                    .ToList()
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job performance for job {JobId}", id);
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<ExecutionStatsDto> GetExecutionStatsDto(DateTime since)
    {
        var executions = await _dbContext.JobExecutions
            .Where(e => e.ExecutionTime >= since)
            .ToListAsync();

        return new ExecutionStatsDto
        {
            Total = executions.Count,
            Successful = executions.Count(e => e.Status == "Success"),
            Failed = executions.Count(e => e.Status == "Failed"),
            SuccessRate = executions.Count > 0 ?
                Math.Round((double)executions.Count(e => e.Status == "Success") / executions.Count * 100, 2) : 0
        };
    }

    private async Task<double> CalculateSuccessRateAsync(DateTime since)
    {
        var totalExecutions = await _dbContext.JobExecutions
            .CountAsync(e => e.ExecutionTime >= since);

        if (totalExecutions == 0) return 0;

        var successfulExecutions = await _dbContext.JobExecutions
            .CountAsync(e => e.ExecutionTime >= since && e.Status == "Success");

        return (double)successfulExecutions / totalExecutions * 100;
    }

    private async Task<double> CalculateAverageExecutionsPerHour(DateTime since)
    {
        var totalExecutions = await _dbContext.JobExecutions
            .CountAsync(e => e.ExecutionTime >= since);

        var hours = (DateTime.UtcNow - since).TotalHours;
        return hours > 0 ? totalExecutions / hours : 0;
    }

    private async Task<List<FailingJobDto>> GetTopFailingJobsDto(DateTime since)
    {
        return await _dbContext.JobExecutions
            .Where(e => e.ExecutionTime >= since && e.Status == "Failed")
            .Include(e => e.Job)
            .GroupBy(e => new { e.JobId, JobName = e.Job != null ? e.Job.Name : "Unknown" })
            .Select(g => new FailingJobDto
            {
                JobId = g.Key.JobId,
                JobName = g.Key.JobName,
                FailureCount = g.Count()
            })
            .OrderByDescending(x => x.FailureCount)
            .Take(5)
            .ToListAsync();
    }

    private async Task<List<HourlyTrendDto>> GetHourlyExecutionTrendDto(DateTime since)
    {
        var executions = await _dbContext.JobExecutions
            .Where(e => e.ExecutionTime >= since)
            .ToListAsync();

        return executions
            .GroupBy(e => new DateTime(e.ExecutionTime.Year, e.ExecutionTime.Month, e.ExecutionTime.Day, e.ExecutionTime.Hour, 0, 0))
            .Select(g => new HourlyTrendDto
            {
                Hour = g.Key,
                TotalExecutions = g.Count(),
                SuccessfulExecutions = g.Count(e => e.Status == "Success"),
                FailedExecutions = g.Count(e => e.Status == "Failed")
            })
            .OrderBy(x => x.Hour)
            .ToList();
    }

    private DateTime? GetNextExecutionTime(string cronExpression)
    {
        try
        {
            // This would require a Cron expression parser
            // For now, return null as it requires additional dependencies
            return null;
        }
        catch
        {
            return null;
        }
    }
}