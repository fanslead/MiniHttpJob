namespace MiniHttpJob.Admin.Configuration;

public class ApmOptions
{
    public bool Enabled { get; set; } = false;
    public string ServiceName { get; set; } = "MiniHttpJob-Admin";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Production";
    
    // Application Insights
    public string? ApplicationInsightsConnectionString { get; set; }
    
    // Elastic APM
    public string? ElasticApmServerUrl { get; set; }
    public string? ElasticApmSecretToken { get; set; }
    
    // Jaeger
    public string? JaegerAgentHost { get; set; }
    public int JaegerAgentPort { get; set; } = 6831;
}