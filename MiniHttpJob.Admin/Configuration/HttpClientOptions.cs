namespace MiniHttpJob.Admin.Configuration;

public class HttpClientOptions
{
    public const string SectionName = "HttpClient";

    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public bool FollowRedirects { get; set; } = true;
    
    // ¡¨Ω”≥ÿ…Ë÷√
    public int MaxConnectionsPerServer { get; set; } = 10;
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(2);
    public bool UseProxy { get; set; } = false;
}