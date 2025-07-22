namespace MiniHttpJob.Admin.Quartz;

/// <summary>
/// 分布式HTTP作业执行器 - 增强版
/// 支持超时控制、重试机制、性能监控和完善的错误处理
/// </summary>
public class DistributedHttpJob : IJob
{
    private readonly ILogger<DistributedHttpJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkerManager _workerManager;
    private readonly IHubContext<Hubs.JobHub, IJobHubClient> _hubContext;
    private readonly IConfiguration _configuration;

    // 性能监控相关
    private static readonly ActivitySource ActivitySource = new("MiniHttpJob.DistributedExecution");

    // 配置常量
    private const int DEFAULT_WORKER_SELECTION_TIMEOUT_SECONDS = 5;
    private const int DEFAULT_JOB_DISPATCH_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_MAX_WORKER_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_SECONDS = 2;

    public DistributedHttpJob(
        ILogger<DistributedHttpJob> logger,
        IServiceProvider serviceProvider,
        IWorkerManager workerManager,
        IHubContext<Hubs.JobHub, IJobHubClient> hubContext,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _workerManager = workerManager ?? throw new ArgumentNullException(nameof(workerManager));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.JobDetail.JobDataMap.GetIntValue("JobId");
        var executionId = Guid.NewGuid().ToString("N")[..8]; // 短UUID用于追踪
        var stopwatch = Stopwatch.StartNew();

        using var activity = ActivitySource.StartActivity($"DistributedJob-{jobId}");
        activity?.SetTag("job.id", jobId);
        activity?.SetTag("execution.id", executionId);

        _logger.LogInformation("Starting distributed job execution: JobId={JobId}, ExecutionId={ExecutionId}, FireTime={FireTime}",
            jobId, executionId, context.FireTimeUtc);

        try
        {
            // 1. 获取作业信息（带超时控制）
            var job = await GetJobWithTimeoutAsync(jobId, context.CancellationToken);
            if (job == null) return;

            // 2. 验证作业状态
            if (!IsJobExecutable(job, jobId)) return;

            // 3. 选择并分发作业（带重试机制）
            var success = await DispatchJobWithRetryAsync(job, context, executionId);

            stopwatch.Stop();

            // 4. 记录执行指标
            await RecordExecutionMetricsAsync(jobId, executionId, success, stopwatch.Elapsed);

            _logger.LogInformation("Distributed job execution completed: JobId={JobId}, ExecutionId={ExecutionId}, Success={Success}, Duration={Duration}ms",
                jobId, executionId, success, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Distributed job execution was cancelled: JobId={JobId}, ExecutionId={ExecutionId}",
                jobId, executionId);

            await RecordFailedExecutionAsync(jobId, executionId, "Job execution was cancelled", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Critical error in distributed job execution: JobId={JobId}, ExecutionId={ExecutionId}, Duration={Duration}ms",
                jobId, executionId, stopwatch.ElapsedMilliseconds);

            await RecordFailedExecutionAsync(jobId, executionId, ex.Message, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 获取作业信息（带超时控制）
    /// </summary>
    private async Task<Job?> GetJobWithTimeoutAsync(int jobId, CancellationToken cancellationToken)
    {
        var timeoutSeconds = _configuration.GetValue("JobScheduler:JobFetchTimeoutSeconds", DEFAULT_WORKER_SELECTION_TIMEOUT_SECONDS);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            var job = await dbContext.Jobs.FindAsync(new object[] { jobId }, timeoutCts.Token);

            if (job == null)
            {
                _logger.LogWarning("Job not found in database: JobId={JobId}", jobId);
                return null;
            }

            return job;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogError("Timeout while fetching job from database: JobId={JobId}, TimeoutSeconds={TimeoutSeconds}",
                jobId, timeoutSeconds);
            return null;
        }
    }

    /// <summary>
    /// 验证作业是否可执行
    /// </summary>
    private bool IsJobExecutable(Job job, int jobId)
    {
        if (job.Status != "Active")
        {
            _logger.LogInformation("Job is not active, skipping execution: JobId={JobId}, Status={Status}",
                jobId, job.Status);
            return false;
        }

        // 添加其他验证逻辑
        if (string.IsNullOrWhiteSpace(job.Url))
        {
            _logger.LogWarning("Job URL is empty, skipping execution: JobId={JobId}", jobId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 分发作业（带重试机制）
    /// </summary>
    private async Task<bool> DispatchJobWithRetryAsync(Job job, IJobExecutionContext context, string executionId)
    {
        var maxRetries = _configuration.GetValue("JobScheduler:MaxWorkerRetryCount", DEFAULT_MAX_WORKER_RETRY_COUNT);
        var retryDelaySeconds = _configuration.GetValue("JobScheduler:RetryDelaySeconds", DEFAULT_RETRY_DELAY_SECONDS);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // 选择Worker
                var worker = await SelectWorkerWithTimeoutAsync(job.Id);
                if (worker == null)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("No available worker found, retrying in {DelaySeconds}s: JobId={JobId}, Attempt={Attempt}",
                            job.Id, retryDelaySeconds, attempt + 1);

                        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds * Math.Pow(2, attempt)), context.CancellationToken);
                        continue;
                    }

                    // 最后一次尝试失败
                    await RecordFailedExecutionAsync(job.Id, executionId, "No available worker found after all retries", TimeSpan.Zero);
                    return false;
                }

                // 分发作业
                var success = await DispatchToWorkerAsync(job, worker, context, executionId);
                if (success)
                {
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Job successfully dispatched after {Attempts} attempts: JobId={JobId}, WorkerId={WorkerId}",
                            job.Id, attempt + 1, worker.WorkerId);
                    }
                    return true;
                }

                // 分发失败，继续重试
                if (attempt < maxRetries)
                {
                    _logger.LogWarning("Failed to dispatch job to worker, retrying: JobId={JobId}, WorkerId={WorkerId}, Attempt={Attempt}",
                        job.Id, worker.WorkerId, attempt + 1);

                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), context.CancellationToken);
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Error during job dispatch attempt {Attempt}, retrying: JobId={JobId}",
                    attempt + 1, job.Id);

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), context.CancellationToken);
            }
        }

        await RecordFailedExecutionAsync(job.Id, executionId, "Failed to dispatch job after all retry attempts", TimeSpan.Zero);
        return false;
    }

    /// <summary>
    /// 选择Worker（带超时控制）
    /// </summary>
    private async Task<WorkerInfo?> SelectWorkerWithTimeoutAsync(int jobId)
    {
        var timeoutSeconds = _configuration.GetValue("JobScheduler:WorkerSelectionTimeoutSeconds", DEFAULT_WORKER_SELECTION_TIMEOUT_SECONDS);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await _workerManager.SelectWorkerForJobAsync(jobId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout while selecting worker: JobId={JobId}, TimeoutSeconds={TimeoutSeconds}",
                jobId, timeoutSeconds);
            return null;
        }
    }

    /// <summary>
    /// 分发作业到Worker
    /// </summary>
    private async Task<bool> DispatchToWorkerAsync(Job job, WorkerInfo worker, IJobExecutionContext context, string executionId)
    {
        var dispatchTimeoutSeconds = _configuration.GetValue("JobScheduler:JobDispatchTimeoutSeconds", DEFAULT_JOB_DISPATCH_TIMEOUT_SECONDS);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(dispatchTimeoutSeconds));

        try
        {
            var command = CreateJobExecutionCommand(job, context, executionId);

            await _hubContext.Clients.Client(worker.WorkerId).ExecuteJob(command);

            _logger.LogInformation("Job dispatched successfully: JobId={JobId}, JobName={JobName}, WorkerId={WorkerId}, InstanceName={InstanceName}, ExecutionId={ExecutionId}",
                job.Id, job.Name, worker.WorkerId, worker.InstanceName, executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch job to worker: JobId={JobId}, WorkerId={WorkerId}, ExecutionId={ExecutionId}",
                job.Id, worker.WorkerId, executionId);

            // 将Worker标记为问题状态（可选）
            await HandleWorkerDispatchFailureAsync(worker, ex);

            return false;
        }
    }

    /// <summary>
    /// 创建作业执行命令
    /// </summary>
    private JobExecutionCommand CreateJobExecutionCommand(Job job, IJobExecutionContext context, string executionId)
    {
        var timeoutSeconds = _configuration.GetValue("JobScheduler:DefaultJobTimeoutSeconds", 30);

        return new JobExecutionCommand
        {
            JobId = job.Id,
            JobName = job.Name,
            HttpMethod = job.HttpMethod,
            Url = job.Url,
            Headers = job.Headers,
            Body = job.Body,
            ScheduledTime = context.FireTimeUtc.DateTime,
            RetryCount = 0,
            Priority = DeterminePriority(job),
            TimeoutSeconds = timeoutSeconds
        };
    }

    /// <summary>
    /// 确定作业优先级
    /// </summary>
    private string DeterminePriority(Job job)
    {
        // 可以基于作业类型、频率等因素确定优先级
        // 这里使用简单的默认逻辑
        return "Normal";
    }

    /// <summary>
    /// 处理Worker分发失败
    /// </summary>
    private Task HandleWorkerDispatchFailureAsync(WorkerInfo worker, Exception ex)
    {
        try
        {
            // 这里可以实现更复杂的Worker状态管理
            // 比如：将Worker标记为临时不可用、发送警告等

            _logger.LogWarning("Worker dispatch failure handled: WorkerId={WorkerId}, Error={Error}",
                worker.WorkerId, ex.Message);
        }
        catch (Exception handlingEx)
        {
            _logger.LogError(handlingEx, "Error while handling worker dispatch failure: WorkerId={WorkerId}",
                worker.WorkerId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录失败的执行
    /// </summary>
    private async Task RecordFailedExecutionAsync(int jobId, string executionId, string errorMessage, TimeSpan duration)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            var execution = new JobExecution
            {
                JobId = jobId,
                ExecutionTime = DateTime.UtcNow,
                Status = "Failed",
                ErrorMessage = TruncateErrorMessage(errorMessage),
                Response = $"ExecutionId: {executionId}, Duration: {duration.TotalMilliseconds}ms"
            };

            dbContext.JobExecutions.Add(execution);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Failed job execution recorded: JobId={JobId}, ExecutionId={ExecutionId}, ErrorMessage={ErrorMessage}",
                jobId, executionId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record failed execution: JobId={JobId}, ExecutionId={ExecutionId}",
                jobId, executionId);
        }
    }

    /// <summary>
    /// 记录执行指标
    /// </summary>
    private Task RecordExecutionMetricsAsync(int jobId, string executionId, bool success, TimeSpan duration)
    {
        try
        {
            // 这里可以集成其他监控系统，如Prometheus、Application Insights等

            var metrics = new
            {
                JobId = jobId,
                ExecutionId = executionId,
                Success = success,
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Execution metrics: {Metrics}", JsonSerializer.Serialize(metrics));

            // TODO: 发送到监控系统
            // await SendToMonitoringSystemAsync(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record execution metrics: JobId={JobId}, ExecutionId={ExecutionId}",
                jobId, executionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 截断错误消息以避免数据库字段溢出
    /// </summary>
    private static string TruncateErrorMessage(string errorMessage)
    {
        const int maxLength = 500;
        return errorMessage.Length > maxLength
            ? errorMessage[..maxLength] + "..."
            : errorMessage;
    }
}