namespace MiniHttpJob.Shared.SignalR;

/// <summary>
/// ×÷ÒµÖ´ÐÐÃüÁî
/// </summary>
public class JobExecutionCommand
{
    public int JobId { get; set; }
    public string JobName { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Headers { get; set; } = "{}";
    public string Body { get; set; } = "";
    public DateTime ScheduledTime { get; set; }
    public int RetryCount { get; set; } = 0;
    public string Priority { get; set; } = "Normal"; // Low, Normal, High
    public int TimeoutSeconds { get; set; } = 30;
}