namespace MiniHttpJob.Shared.DTOs;

public class JobExecutionDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public DateTime ExecutionTime { get; set; }
    public string Status { get; set; } = null!;
    public string ErrorMessage { get; set; } = "";
    public string Response { get; set; } = "";
    public JobDto? Job { get; set; }
}