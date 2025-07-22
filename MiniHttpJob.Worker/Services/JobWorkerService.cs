namespace MiniHttpJob.Worker.Services;

/// <summary>
/// Worker��̨���񣬸�������ҵ���к�ִ����ҵ
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

        // ����SignalR����
        try
        {
            await _signalRClientService.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR client service");
            return;
        }

        // ������ҵ����ѭ��
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
            // ����ֹͣ
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
                // �ȴ��ź��������Ʋ���ִ������
                await _semaphore.WaitAsync(stoppingToken);

                try
                {
                    // �Ӷ����л�ȡ��ҵ
                    var command = await _jobQueueService.DequeueJobAsync(stoppingToken);
                    if (command == null)
                    {
                        continue;
                    }

                    // �첽ִ����ҵ�����ȴ����
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
                    // �����ȡ��ҵʧ�ܣ��ͷ��ź���
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

                // ��������ʱ�ȴ�һ��ʱ���ټ���
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

            // ִ����ҵ
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
            // �������б����Ƴ���ҵ
            _jobQueueService.RemoveRunningJob(command.JobId);

            // ����ִ�н����Admin
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

            // ����Worker״̬
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