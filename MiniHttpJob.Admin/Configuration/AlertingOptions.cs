namespace MiniHttpJob.Admin.Configuration;

public class AlertingOptions
{
    public bool Enabled { get; set; } = false;
    public int FailureThresholdPercentage { get; set; } = 10; // 10%失败率触发警报
    public TimeSpan FailureThresholdTimeWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConsecutiveFailures { get; set; } = 5;
    
    // 通知设置
    public EmailOptions Email { get; set; } = new();
    public WebhookOptions Webhook { get; set; } = new();
}