namespace MiniHttpJob.Shared.DTOs;

public class JobDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Headers { get; set; } = "{}";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string ExecutionType { get; set; } = "Auto"; // Auto, Local, Distributed
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}