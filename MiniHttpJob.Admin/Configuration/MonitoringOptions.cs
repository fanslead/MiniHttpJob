namespace MiniHttpJob.Admin.Configuration;

/// <summary>
/// 监控和警报配置选项
/// </summary>
public class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    
    public bool EnableHealthChecks { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = false;
    
    // APM设置
    public ApmOptions Apm { get; set; } = new();
    
    // 警报设置
    public AlertingOptions Alerting { get; set; } = new();
}