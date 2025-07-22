namespace MiniHttpJob.Shared.DTOs;

public class JobHistoryResponseDto
{
    public List<JobExecutionDto> Data { get; set; } = new();
    public int Count { get; set; }
}