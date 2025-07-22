namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// ��ҵִ�н��
/// </summary>
public class JobExecutionResult
{
    public int JobId { get; set; }
    public string WorkerId { get; set; } = null!;
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Response { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
    public DateTime CompletionTime { get; set; } = DateTime.UtcNow;
}