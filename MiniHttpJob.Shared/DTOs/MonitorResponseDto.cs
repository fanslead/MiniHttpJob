namespace MiniHttpJob.Shared.DTOs;

public class DashboardResponseDto
{
    public DashboardSummaryDto Summary { get; set; } = null!;
    public List<RecentExecutionDto> RecentExecutions { get; set; } = new();
    public SystemInfoDto SystemInfo { get; set; } = null!;
}

public class DashboardSummaryDto
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int PausedJobs { get; set; }
    public double SuccessRate { get; set; }
    public int ExecutionsLast24Hours { get; set; }
    public int ExecutionsLastHour { get; set; }
    public double AverageExecutionsPerHour { get; set; }
}

public class RecentExecutionDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string JobName { get; set; } = null!;
    public DateTime ExecutionTime { get; set; }
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}

public class SystemInfoDto
{
    public DateTime ServerTime { get; set; }
    public string Environment { get; set; } = null!;
    public string MachineName { get; set; } = null!;
    public int ProcessorCount { get; set; }
    public long WorkingSet { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class MonitorStatisticsDto
{
    public ExecutionStatsGroupDto ExecutionStats { get; set; } = null!;
    public List<JobStatusCountDto> JobsByStatus { get; set; } = new();
    public List<FailingJobDto> TopFailingJobs { get; set; } = new();
    public List<HourlyTrendDto> HourlyExecutionTrend { get; set; } = new();
}

public class ExecutionStatsGroupDto
{
    public ExecutionStatsDto Last24Hours { get; set; } = null!;
    public ExecutionStatsDto Last7Days { get; set; } = null!;
    public ExecutionStatsDto Last30Days { get; set; } = null!;
}

public class JobStatusCountDto
{
    public string Status { get; set; } = null!;
    public int Count { get; set; }
}

public class FailingJobDto
{
    public int JobId { get; set; }
    public string JobName { get; set; } = null!;
    public int FailureCount { get; set; }
}

public class HourlyTrendDto
{
    public DateTime Hour { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
}

public class JobPerformanceDto
{
    public JobInfoDto JobInfo { get; set; } = null!;
    public JobExecutionSummaryDto ExecutionSummary { get; set; } = null!;
    public List<DailyTrendDto> DailyTrend { get; set; } = new();
    public List<RecentFailureDto> RecentFailures { get; set; } = new();
}

public class JobInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public string Url { get; set; } = null!;
}

public class JobExecutionSummaryDto
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public DateTime? LastExecution { get; set; }
    public DateTime? NextExecution { get; set; }
}

public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
}

public class RecentFailureDto
{
    public DateTime ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }
}