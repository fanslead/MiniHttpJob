namespace MiniHttpJob.Shared.DTOs;

public class StatisticsDto
{
    public Dictionary<string, int> ExecutionsByHour { get; set; } = new();
    public Dictionary<string, int> ExecutionsByDay { get; set; } = new();
    public Dictionary<string, int> SuccessRateByJob { get; set; } = new();
    public List<JobDto> TopFailedJobs { get; set; } = new();
}