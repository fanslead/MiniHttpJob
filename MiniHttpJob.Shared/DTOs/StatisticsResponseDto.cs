namespace MiniHttpJob.Shared.DTOs;

public class StatisticsResponseDto
{
    public PeriodDto Period { get; set; } = null!;
    public ExecutionSummaryDto Summary { get; set; } = null!;
    public Dictionary<string, HourlyExecutionDto> ExecutionsByHour { get; set; } = new();
    public List<JobExecutionStatsDto> ExecutionsByJob { get; set; } = new();
    public List<JobExecutionStatsDto> TopFailingJobs { get; set; } = new();
}

public class PeriodDto
{
    public int Hours { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class ExecutionSummaryDto
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
}

public class HourlyExecutionDto
{
    public int Total { get; set; }
    public int Successful { get; set; }
}

public class JobExecutionStatsDto
{
    public int JobId { get; set; }
    public string JobName { get; set; } = null!;
    public int Total { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double SuccessRate { get; set; }
    public double AvgDuration { get; set; }
}