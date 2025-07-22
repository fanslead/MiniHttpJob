namespace MiniHttpJob.Shared.DTOs;

public class UpdateJobDto
{
    [Required(ErrorMessage = "Job name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Job name must be between 1 and 100 characters")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Cron expression is required")]
    public string CronExpression { get; set; } = null!;

    [Required(ErrorMessage = "HTTP method is required")]
    [RegularExpression(@"^(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)$", ErrorMessage = "Invalid HTTP method")]
    public string HttpMethod { get; set; } = null!;

    [Required(ErrorMessage = "URL is required")]
    [Url(ErrorMessage = "Invalid URL format")]
    public string Url { get; set; } = null!;

    public string Headers { get; set; } = "{}";
    public string Body { get; set; } = "";

    /// <summary>
    /// 作业执行类型: Auto(自动选择), Local(本地执行), Distributed(分布式执行)
    /// </summary>
    [RegularExpression(@"^(Auto|Local|Distributed)$", ErrorMessage = "ExecutionType must be Auto, Local, or Distributed")]
    public string ExecutionType { get; set; } = "Auto";
}