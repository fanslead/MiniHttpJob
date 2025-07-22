namespace MiniHttpJob.Worker.Services;

public interface IJobExecutorService
{
    Task<JobExecutionResult> ExecuteJobAsync(JobExecutionCommand command, CancellationToken cancellationToken = default);
}

public class JobExecutorService : IJobExecutorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JobExecutorService> _logger;
    private readonly IConfiguration _configuration;

    // Constants for configuration keys
    private const string DefaultTimeoutSecondsKey = "Worker:DefaultTimeoutSeconds";
    private const string MaxRetriesKey = "Worker:MaxRetries";
    private const string RetryDelaySecondsKey = "Worker:RetryDelaySeconds";

    public JobExecutorService(
        IHttpClientFactory httpClientFactory,
        ILogger<JobExecutorService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<JobExecutionResult> ExecuteJobAsync(JobExecutionCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var stopwatch = Stopwatch.StartNew();
        var executionTime = DateTime.UtcNow;
        var timeoutSeconds = command.TimeoutSeconds > 0 ? command.TimeoutSeconds :
            _configuration.GetValue(DefaultTimeoutSecondsKey, 30);

        var maxRetries = _configuration.GetValue(MaxRetriesKey, 3);
        var retryDelaySeconds = _configuration.GetValue(RetryDelaySecondsKey, 2);

        try
        {
            _logger.LogInformation("Executing job: JobId={JobId}, JobName={JobName}, Method={HttpMethod}, Url={Url}",
                command.JobId, command.JobName, command.HttpMethod, command.Url);

            // Execute with retry logic
            HttpExecutionResult result = null!;
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    result = await ExecuteHttpRequestAsync(command, timeoutSeconds, cancellationToken);
                    break; // Success, exit retry loop
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    var delay = TimeSpan.FromSeconds(retryDelaySeconds * Math.Pow(2, attempt)); // Exponential backoff

                    _logger.LogWarning("HTTP request attempt {AttemptNumber} failed. Retrying in {Delay}s. Error: {Error}",
                        attempt + 1, delay.TotalSeconds, ex.Message);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < maxRetries)
                {
                    lastException = ex;
                    var delay = TimeSpan.FromSeconds(retryDelaySeconds * Math.Pow(2, attempt));

                    _logger.LogWarning("HTTP request attempt {AttemptNumber} timed out. Retrying in {Delay}s.",
                        attempt + 1, delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            // If we still don't have a result, throw the last exception
            if (result == null && lastException != null)
                throw lastException;

            stopwatch.Stop();

            var finalResult = new JobExecutionResult
            {
                JobId = command.JobId,
                Success = result.IsSuccessStatusCode,
                StatusCode = (int)result.StatusCode,
                Response = TruncateResponseIfNeeded(result.ResponseBody),
                ErrorMessage = result.IsSuccessStatusCode ? "" : (result.ReasonPhrase ?? "Unknown error"),
                Duration = stopwatch.Elapsed,
                ExecutionTime = executionTime,
                CompletionTime = DateTime.UtcNow,
                WorkerId = Environment.MachineName
            };

            _logger.LogInformation("Job executed successfully: JobId={JobId}, StatusCode={StatusCode}, Duration={Duration}ms",
                command.JobId, result.StatusCode, stopwatch.ElapsedMilliseconds);

            return finalResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning("Job execution was cancelled: JobId={JobId}", command.JobId);

            return CreateErrorResult(command, stopwatch.Elapsed, executionTime, "Job execution was cancelled");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning("Job execution timed out: JobId={JobId}, Timeout={TimeoutSeconds}s",
                command.JobId, timeoutSeconds);

            return CreateErrorResult(command, stopwatch.Elapsed, executionTime,
                $"Request timed out after {timeoutSeconds} seconds");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "HTTP request failed for job: JobId={JobId}", command.JobId);

            return CreateErrorResult(command, stopwatch.Elapsed, executionTime,
                $"HTTP request failed: {TruncateErrorMessage(ex.Message)}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Job execution failed with unexpected error: JobId={JobId}", command.JobId);

            return CreateErrorResult(command, stopwatch.Elapsed, executionTime,
                $"Unexpected error: {TruncateErrorMessage(ex.Message)}");
        }
    }

    private async Task<HttpExecutionResult> ExecuteHttpRequestAsync(
        JobExecutionCommand command,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("JobClient");

        // Set timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        // Create HTTP request
        var request = new HttpRequestMessage(new HttpMethod(command.HttpMethod), command.Url);

        // Add request body for applicable methods
        if (!string.IsNullOrEmpty(command.Body) &&
            IsMethodWithBody(command.HttpMethod))
        {
            var contentType = ExtractContentTypeFromHeaders(command.Headers) ?? "application/json";
            request.Content = new StringContent(command.Body, System.Text.Encoding.UTF8, contentType);
        }

        // Add custom headers
        AddCustomHeaders(request, command.Headers);

        // Execute HTTP request
        var response = await client.SendAsync(request, timeoutCts.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        return new HttpExecutionResult
        {
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            StatusCode = response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            ResponseBody = responseBody
        };
    }

    private static bool IsMethodWithBody(string httpMethod)
    {
        return httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }

    private string? ExtractContentTypeFromHeaders(string? headersJson)
    {
        if (string.IsNullOrEmpty(headersJson) || headersJson == "{}")
            return null;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            return headers?.FirstOrDefault(h =>
                h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract content type from headers: {Headers}", headersJson);
            return null;
        }
    }

    private void AddCustomHeaders(HttpRequestMessage request, string? headersJson)
    {
        if (string.IsNullOrEmpty(headersJson) || headersJson == "{}")
            return;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            if (headers == null) return;

            foreach (var header in headers)
            {
                try
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content != null)
                    {
                        // Content-Type is already handled in content creation
                        continue;
                    }
                    else if (IsContentHeader(header.Key) && request.Content != null)
                    {
                        request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    else
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add header {HeaderKey}={HeaderValue}", header.Key, header.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse headers: {Headers}", headersJson);
        }
    }

    private static bool IsContentHeader(string headerName)
    {
        var contentHeaders = new[] { "Content-Length", "Content-Type", "Content-Encoding", "Content-Language", "Content-MD5" };
        return contentHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }

    private static string TruncateResponseIfNeeded(string response)
    {
        const int maxResponseLength = 2000;
        if (string.IsNullOrEmpty(response))
            return string.Empty;

        if (response.Length <= maxResponseLength)
            return response;

        return response.Substring(0, maxResponseLength) + "... (truncated)";
    }

    private static string TruncateErrorMessage(string message)
    {
        const int maxErrorLength = 500;
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        if (message.Length <= maxErrorLength)
            return message;

        return message.Substring(0, maxErrorLength) + "... (truncated)";
    }

    private JobExecutionResult CreateErrorResult(
        JobExecutionCommand command,
        TimeSpan duration,
        DateTime executionTime,
        string errorMessage)
    {
        return new JobExecutionResult
        {
            JobId = command.JobId,
            Success = false,
            ErrorMessage = errorMessage,
            Duration = duration,
            ExecutionTime = executionTime,
            CompletionTime = DateTime.UtcNow,
            WorkerId = Environment.MachineName
        };
    }

    private record HttpExecutionResult
    {
        public bool IsSuccessStatusCode { get; init; }
        public System.Net.HttpStatusCode StatusCode { get; init; }
        public string? ReasonPhrase { get; init; }
        public string ResponseBody { get; init; } = string.Empty;
    }
}