namespace MiniHttpJob.Admin.Configuration;

public class WebhookOptions
{
    public bool Enabled { get; set; } = false;
    public string Url { get; set; } = "";
    public string Secret { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}