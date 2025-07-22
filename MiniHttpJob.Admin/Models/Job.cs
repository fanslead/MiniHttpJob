namespace MiniHttpJob.Admin.Models;

public class Job
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Headers { get; set; } = "{}"; // JSON string for headers
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Active"; // Active, Paused
    public string ExecutionType { get; set; } = "Auto"; // Auto, Local, Distributed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}