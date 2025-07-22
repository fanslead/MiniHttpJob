namespace MiniHttpJob.Worker.Services;

/// <summary>
/// Worker后台服务，负责处理作业队列和执行作业
/// </summary>
public class JobWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobWorkerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IJobQueueService _jobQueueService;
    private readonly ISignalRClientService _signalRClientService;
    private readonly SemaphoreSlim _semaphore;

    public JobWorkerService(
        IServiceProvider serviceProvider,
        ILogger<JobWorkerService> logger,
        IConfiguration configuration,
        IJobQueueService jobQueueService,
        ISignalRClientService signalRClientService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _jobQueueService = jobQueueService;
        _signalRClientService = signalRClientService;

        var maxConcurrentJobs = _configuration.GetValue("Worker:MaxConcurrentJobs", 10);
        _semaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Worker Service started");

        // 启动SignalR连接
        try
        {
            await _signalRClientService.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR client service");
            return;
        }

        // 启动作业处理循环
        var processingTasks = new List<Task>();
        var maxConcurrentJobs = _configuration.GetValue("Worker:MaxConcurrentJobs", 10);

        for (int i = 0; i < maxConcurrentJobs; i++)
        {
            processingTasks.Add(ProcessJobsAsync(stoppingToken));
        }

        try
        {
            await Task.WhenAll(processingTasks);
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in job processing tasks");
        }
        finally
        {
            await _signalRClientService.StopAsync();
            _logger.LogInformation("Job Worker Service stopped");
        }
    }

    private async Task ProcessJobsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待信号量，限制并发执行数量
                await _semaphore.WaitAsync(stoppingToken);

                try
                {
                    // 从队列中获取作业
                    var command = await _jobQueueService.DequeueJobAsync(stoppingToken);
                    if (command == null)
                    {
                        continue;
                    }

                    // 异步执行作业，不等待完成
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteJobAsync(command, stoppingToken);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }, stoppingToken);
                }
                catch
                {
                    // 如果获取作业失败，释放信号量
                    _semaphore.Release();
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processing loop");

                // 发生错误时等待一段时间再继续
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ExecuteJobAsync(JobExecutionCommand command, CancellationToken stoppingToken)
    {
        JobExecutionResult? result = null;

        try
        {
            _logger.LogInformation("Starting job execution: JobId={JobId}, JobName={JobName}",
                command.JobId, command.JobName);

            using var scope = _serviceProvider.CreateScope();
            var executorService = scope.ServiceProvider.GetRequiredService<IJobExecutorService>();

            // 执行作业
            result = await executorService.ExecuteJobAsync(command, stoppingToken);

            _logger.LogInformation("Job execution completed: JobId={JobId}, Success={Success}, Duration={Duration}ms",
                command.JobId, result.Success, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job: JobId={JobId}", command.JobId);

            result = new JobExecutionResult
            {
                JobId = command.JobId,
                Success = false,
                ErrorMessage = ex.Message.Length > 500 ? ex.Message.Substring(0, 500) + "..." : ex.Message,
                Duration = TimeSpan.Zero,
                ExecutionTime = DateTime.UtcNow,
                CompletionTime = DateTime.UtcNow
            };
        }
        finally
        {
            // 从运行列表中移除作业
            _jobQueueService.RemoveRunningJob(command.JobId);

            // 报告执行结果给Admin
            if (result != null && _signalRClientService.IsConnected)
            {
                try
                {
                    await _signalRClientService.ReportJobCompletionAsync(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to report job completion: JobId={JobId}", command.JobId);
                }
            }

            // 更新Worker状态
            try
            {
                var status = new WorkerStatusUpdate
                {
                    Status = WorkerStatus.Idle,
                    Capacity = new WorkerCapacity
                    {
                        MaxConcurrentJobs = _configuration.GetValue("Worker:MaxConcurrentJobs", 10),
                        CurrentRunningJobs = await _jobQueueService.GetRunningJobCountAsync(),
                        QueueSize = await _jobQueueService.GetQueueSizeAsync()
                    }
                };

                await _signalRClientService.UpdateStatusAsync(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update worker status");
            }
        }
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
    }
}