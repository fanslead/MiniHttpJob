namespace MiniHttpJob.Shared.DTOs;

public class DashboardDto
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int PausedJobs { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public List<JobDto> RecentJobs { get; set; } = new();
    public List<JobExecutionDto> RecentExecutions { get; set; } = new();
}