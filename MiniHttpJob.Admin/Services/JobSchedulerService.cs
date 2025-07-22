namespace MiniHttpJob.Admin.Services;

public class JobSchedulerService : IJobSchedulerService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IJobFactory _jobFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobSchedulerService> _logger;
    private readonly IWorkerManager _workerManager;
    private readonly IHubContext<Hubs.JobHub, IJobHubClient> _hubContext;
    private readonly JobSchedulerOptions _options;
    private IScheduler _scheduler = null!;

    public JobSchedulerService(
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory,
        IServiceProvider serviceProvider,
        ILogger<JobSchedulerService> logger,
        IWorkerManager workerManager,
        IHubContext<Hubs.JobHub, IJobHubClient> hubContext,
        IOptions<JobSchedulerOptions> options)
    {
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerManager = workerManager;
        _hubContext = hubContext;
        _options = options.Value;
    }

    public async Task StartAsync()
    {
        _scheduler = await _schedulerFactory.GetScheduler();
        _scheduler.JobFactory = _jobFactory;

        await LoadJobsFromDatabaseAsync();
        await _scheduler.Start();

        _logger.LogInformation("Job scheduler started successfully with clustering {ClusteringStatus}.",
            _options.EnableClustering ? "enabled" : "disabled");
    }

    public async Task ScheduleJobAsync(Job job)
    {
        if (job.Status != "Active")
            return;

        // 决定使用哪种作业类型
        var jobType = DetermineJobType(job);

        var jobDetail = JobBuilder.Create(jobType)
            .WithIdentity(job.Id.ToString())
            .UsingJobData("JobId", job.Id)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{job.Id}")
            .WithCronSchedule(job.CronExpression)
            .Build();

        await _scheduler.ScheduleJob(jobDetail, trigger);

        _logger.LogInformation("Job {JobId} ({JobName}) scheduled successfully using {JobType} with execution type {ExecutionType}.",
            job.Id, job.Name, jobType.Name, job.ExecutionType);
    }

    /// <summary>
    /// 根据作业配置和系统设置决定使用哪种作业类型
    /// </summary>
    private Type DetermineJobType(Job job)
    {
        return job.ExecutionType switch
        {
            "Local" => typeof(HttpJob),
            "Distributed" => typeof(DistributedHttpJob),
            "Auto" => DetermineAutoJobType(),
            _ => DetermineAutoJobType()
        };
    }

    /// <summary>
    /// 自动决定作业类型
    /// </summary>
    private Type DetermineAutoJobType()
    {
        // 如果启用了集群模式，且有可用的Worker，使用分布式作业
        if (_options.EnableClustering)
        {
            // Replace the following line in the DetermineAutoJobType method:
            var availableWorkers = _workerManager.GetAvailableWorkerInfoAsync().Result;
            if (availableWorkers.Any())
            {
                _logger.LogDebug("Auto-selecting DistributedHttpJob due to clustering enabled with available workers");
                return typeof(DistributedHttpJob);
            }

            _logger.LogDebug("Auto-selecting HttpJob despite clustering enabled due to no available workers");
            return typeof(HttpJob);
        }

        _logger.LogDebug("Auto-selecting HttpJob due to clustering disabled");
        return typeof(HttpJob);
    }

    public async Task UnscheduleJobAsync(int jobId)
    {
        var jobKey = new JobKey(jobId.ToString());
        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.DeleteJob(jobKey);
            _logger.LogInformation($"Job {jobId} unscheduled successfully.");
        }
    }

    public async Task PauseJobAsync(int jobId)
    {
        var jobKey = new JobKey(jobId.ToString());
        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.PauseJob(jobKey);
            _logger.LogInformation($"Job {jobId} paused successfully.");
        }
    }

    public async Task ResumeJobAsync(int jobId)
    {
        var jobKey = new JobKey(jobId.ToString());
        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.ResumeJob(jobKey);
            _logger.LogInformation($"Job {jobId} resumed successfully.");
        }
        else
        {
            // If job doesn't exist in scheduler, reschedule it
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            var job = await dbContext.Jobs.FindAsync(jobId);
            if (job != null && job.Status == "Active")
            {
                await ScheduleJobAsync(job);
            }
        }
    }

    public async Task LoadJobsFromDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

        var activeJobs = await dbContext.Jobs
            .Where(j => j.Status == "Active")
            .ToListAsync();

        foreach (var job in activeJobs)
        {
            try
            {
                await ScheduleJobAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to schedule job {job.Id} ({job.Name}).");
            }
        }

        _logger.LogInformation($"Loaded {activeJobs.Count} active jobs from database.");
    }

    /// <summary>
    /// 手动触发作业执行
    /// </summary>
    public async Task<bool> TriggerJobAsync(int jobId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

            var job = await dbContext.Jobs.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found", jobId);
                return false;
            }

            // 选择可用的Worker
            var worker = await _workerManager.SelectWorkerForJobAsync(jobId);
            if (worker == null)
            {
                _logger.LogWarning("No available worker found for job {JobId}", jobId);
                return false;
            }

            // 创建作业执行命令
            var command = new JobExecutionCommand
            {
                JobId = job.Id,
                JobName = job.Name,
                HttpMethod = job.HttpMethod,
                Url = job.Url,
                Headers = job.Headers,
                Body = job.Body,
                ScheduledTime = DateTime.UtcNow,
                RetryCount = 0
            };

            // 通过SignalR发送作业执行命令给Worker
            await _hubContext.Clients.Client(worker.WorkerId).ExecuteJob(command);

            _logger.LogInformation("Job {JobId} triggered and sent to worker {WorkerId}", jobId, worker.WorkerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger job {JobId}", jobId);
            return false;
        }
    }
}