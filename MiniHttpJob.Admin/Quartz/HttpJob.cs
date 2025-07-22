namespace MiniHttpJob.Admin.Quartz;

public class HttpJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HttpJob> _logger;

    public HttpJob(IServiceProvider serviceProvider, ILogger<HttpJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClientOptions = scope.ServiceProvider.GetService<IOptions<HttpClientOptions>>()?.Value ?? new HttpClientOptions();

        var jobId = context.JobDetail.Key.Name;

        try
        {
            var job = await dbContext.Jobs.FindAsync(int.Parse(jobId));

            if (job == null)
            {
                _logger.LogError("Job with ID {JobId} not found", jobId);
                return;
            }

            _logger.LogInformation("Starting execution of job {JobId} ({JobName}) - {Method} {Url}",
                jobId, job.Name, job.HttpMethod, job.Url);

            var client = httpClientFactory.CreateClient("JobClient");

            // Configure timeout
            client.Timeout = TimeSpan.FromSeconds(httpClientOptions.TimeoutSeconds);

            var executionResult = await ExecuteHttpRequestWithRetry(client, job, httpClientOptions.MaxRetries);

            // Record execution
            await RecordJobExecution(dbContext, job.Id, executionResult, stopwatch.Elapsed);

            _logger.LogInformation("Job {JobId} completed in {Duration}ms with status {Status}",
                jobId, stopwatch.ElapsedMilliseconds, executionResult.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error executing job {JobId}", jobId);

            // Try to record the failure if possible
            try
            {
                if (int.TryParse(jobId, out var id))
                {
                    await RecordJobExecution(dbContext, id, new ExecutionResult
                    {
                        Status = "Failed",
                        ErrorMessage = $"Fatal error: {ex.Message}",
                        Response = "",
                        StatusCode = 0
                    }, stopwatch.Elapsed);
                }
            }
            catch (Exception recordEx)
            {
                _logger.LogError(recordEx, "Failed to record job execution failure for job {JobId}", jobId);
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<ExecutionResult> ExecuteHttpRequestWithRetry(HttpClient client, Job job, int maxRetries)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                var result = await ExecuteHttpRequest(client, job);

                if (result.Status == "Success" || attempt == maxRetries + 1)
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Job {JobId} succeeded on attempt {Attempt}", job.Id, attempt);
                    }
                    return result;
                }

                _logger.LogWarning("Job {JobId} failed on attempt {Attempt}, will retry. Status: {StatusCode}",
                    job.Id, attempt, result.StatusCode);

                if (attempt <= maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))); // Exponential backoff
                }

                lastException = new HttpRequestException($"HTTP request failed with status {result.StatusCode}");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Job {JobId} failed on attempt {Attempt}: {Message}",
                    job.Id, attempt, ex.Message);

                if (attempt <= maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))); // Exponential backoff
                }
            }
        }

        return new ExecutionResult
        {
            Status = "Failed",
            ErrorMessage = lastException?.Message ?? "Unknown error",
            Response = "",
            StatusCode = 0
        };
    }

    private async Task<ExecutionResult> ExecuteHttpRequest(HttpClient client, Job job)
    {
        var request = new HttpRequestMessage(new HttpMethod(job.HttpMethod), job.Url);

        // Set content if body is not empty
        if (!string.IsNullOrEmpty(job.Body))
        {
            request.Content = new StringContent(job.Body, Encoding.UTF8, "application/json");
        }

        // Add headers
        var headers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(job.Headers);
        if (headers != null)
        {
            foreach (var header in headers)
            {
                try
                {
                    // Handle Content-Type separately
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content != null)
                    {
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(header.Value);
                    }
                    else
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to add header {HeaderKey}={HeaderValue}: {Error}",
                        header.Key, header.Value, ex.Message);
                }
            }
        }

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        return new ExecutionResult
        {
            Status = response.IsSuccessStatusCode ? "Success" : "Failed",
            ErrorMessage = response.IsSuccessStatusCode ? "" : (response.ReasonPhrase ?? "Unknown error"),
            Response = responseBody.Length > 2000 ? responseBody.Substring(0, 2000) + "..." : responseBody,
            StatusCode = (int)response.StatusCode
        };
    }

    private async Task RecordJobExecution(JobDbContext dbContext, int jobId, ExecutionResult result, TimeSpan duration)
    {
        var execution = new JobExecution
        {
            JobId = jobId,
            ExecutionTime = DateTime.UtcNow,
            Status = result.Status,
            ErrorMessage = result.ErrorMessage.Length > 1000 ? result.ErrorMessage.Substring(0, 1000) + "..." : result.ErrorMessage,
            Response = result.Response
        };

        dbContext.JobExecutions.Add(execution);
        await dbContext.SaveChangesAsync();
    }

    private class ExecutionResult
    {
        public string Status { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string Response { get; set; } = "";
        public int StatusCode { get; set; }
    }
}