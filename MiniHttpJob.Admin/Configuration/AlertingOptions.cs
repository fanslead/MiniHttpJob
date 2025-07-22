namespace MiniHttpJob.Admin.Configuration;

public class AlertingOptions
{
    public bool Enabled { get; set; } = false;
    public int FailureThresholdPercentage { get; set; } = 10; // 10%ʧ���ʴ�������
    public TimeSpan FailureThresholdTimeWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConsecutiveFailures { get; set; } = 5;
    
    // ֪ͨ����
    public EmailOptions Email { get; set; } = new();
    public WebhookOptions Webhook { get; set; } = new();
}