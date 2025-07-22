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
                    Name = "�����������",
                    CronExpression = "0/30 * * * * ?", // ÿ30��ִ��һ��
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
                    Name = "����ͬ������",
                    CronExpression = "0 0/5 * * * ?", // ÿ5����ִ��һ��
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
                    Name = "ÿ�ձ�������",
                    CronExpression = "0 0 9 * * ?", // ÿ������9��ִ��
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