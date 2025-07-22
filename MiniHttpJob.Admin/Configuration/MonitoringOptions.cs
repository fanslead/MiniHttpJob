namespace MiniHttpJob.Admin.Configuration;

/// <summary>
/// ��غ;�������ѡ��
/// </summary>
public class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    
    public bool EnableHealthChecks { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = false;
    
    // APM����
    public ApmOptions Apm { get; set; } = new();
    
    // ��������
    public AlertingOptions Alerting { get; set; } = new();
}