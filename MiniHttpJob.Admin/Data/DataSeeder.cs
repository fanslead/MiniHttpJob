namespace MiniHttpJob.Admin.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(JobDbContext context)
    {
        if (!context.Jobs.Any())
        {
            var sampleJobs = new[]
            {
                new Job
                {
                    Name = "健康检查任务",
                    CronExpression = "0/30 * * * * ?", // 每30秒执行一次
                    HttpMethod = "GET",
                    Url = "https://localhost:5001/api/test/status",
                    Headers = "{}",
                    Body = "",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Job
                {
                    Name = "数据同步任务",
                    CronExpression = "0 0/5 * * * ?", // 每5分钟执行一次
                    HttpMethod = "POST",
                    Url = "https://localhost:5001/api/test/webhook",
                    Headers = "{\"Content-Type\": \"application/json\", \"Authorization\": \"Bearer sample-token\"}",
                    Body = "{\"action\": \"sync\", \"timestamp\": \"" + DateTime.UtcNow.ToString("O") + "\"}",
                    Status = "Paused",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Job
                {
                    Name = "每日报告任务",
                    CronExpression = "0 0 9 * * ?", // 每天上午9点执行
                    HttpMethod = "POST",
                    Url = "https://localhost:5001/api/test/webhook",
                    Headers = "{\"Content-Type\": \"application/json\"}",
                    Body = "{\"type\": \"daily-report\", \"date\": \"" + DateTime.UtcNow.ToString("yyyy-MM-dd") + "\"}",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            context.Jobs.AddRange(sampleJobs);
            await context.SaveChangesAsync();
        }
    }
}