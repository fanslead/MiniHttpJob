namespace MiniHttpJob.Admin.Services;

public class JobService : IJobService
{
    private readonly JobDbContext _dbContext;
    private readonly IJobSchedulerService _schedulerService;
    private readonly ILogger<JobService> _logger;

    // Constants for job statuses
    private const string JobStatusActive = "Active";
    private const string JobStatusPaused = "Paused";

    public JobService(JobDbContext dbContext, IJobSchedulerService schedulerService, ILogger<JobService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<JobDto> CreateJobAsync(CreateJobDto createJobDto)
    {
        if (createJobDto == null)
            throw new ArgumentNullException(nameof(createJobDto));

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var job = new Job
            {
                Name = createJobDto.Name,
                CronExpression = createJobDto.CronExpression,
                HttpMethod = createJobDto.HttpMethod,
                Url = createJobDto.Url,
                Headers = createJobDto.Headers ?? "{}",
                Body = createJobDto.Body ?? "",
                Status = JobStatusActive,
                ExecutionType = createJobDto.ExecutionType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Jobs.Add(job);
            await _dbContext.SaveChangesAsync();

            // Schedule the job
            await _schedulerService.ScheduleJobAsync(job);

            await transaction.CommitAsync();

            _logger.LogInformation("Job created successfully with ID {JobId} and name '{JobName}' using {ExecutionType} execution",
                job.Id, job.Name, job.ExecutionType);
            return MapToDto(job);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create job with name '{JobName}'", createJobDto.Name);
            throw;
        }
    }

    public async Task<JobDto?> GetJobAsync(int id)
    {
        if (id <= 0)
            return null;

        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            return job == null ? null : MapToDto(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve job with ID {JobId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<JobDto>> GetJobsAsync()
    {
        try
        {
            var jobs = await _dbContext.Jobs
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
            return jobs.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve jobs list");
            throw;
        }
    }

    public async Task<JobDto?> UpdateJobAsync(int id, UpdateJobDto updateJobDto)
    {
        if (id <= 0)
            return null;

        if (updateJobDto == null)
            throw new ArgumentNullException(nameof(updateJobDto));

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null)
                return null;

            var oldStatus = job.Status;
            job.Name = updateJobDto.Name;
            job.CronExpression = updateJobDto.CronExpression;
            job.HttpMethod = updateJobDto.HttpMethod;
            job.Url = updateJobDto.Url;
            job.Headers = updateJobDto.Headers ?? "{}";
            job.Body = updateJobDto.Body ?? "";
            job.ExecutionType = updateJobDto.ExecutionType;
            job.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // Reschedule the job if it's active (to apply new execution type)
            if (job.Status == JobStatusActive)
            {
                await _schedulerService.UnscheduleJobAsync(job.Id);
                await _schedulerService.ScheduleJobAsync(job);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Job updated successfully with ID {JobId}", job.Id);
            return MapToDto(job);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update job with ID {JobId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(int id)
    {
        if (id <= 0)
            return false;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null)
                return false;

            // Unschedule the job first
            await _schedulerService.UnscheduleJobAsync(job.Id);

            _dbContext.Jobs.Remove(job);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Job deleted successfully with ID {JobId}", job.Id);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to delete job with ID {JobId}", id);
            throw;
        }
    }

    public async Task<bool> PauseJobAsync(int id)
    {
        if (id <= 0)
            return false;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null)
                return false;

            if (job.Status == JobStatusPaused)
            {
                _logger.LogWarning("Job with ID {JobId} is already paused", id);
                return true;
            }

            job.Status = JobStatusPaused;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _schedulerService.PauseJobAsync(job.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Job paused successfully with ID {JobId}", job.Id);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to pause job with ID {JobId}", id);
            throw;
        }
    }

    public async Task<bool> ResumeJobAsync(int id)
    {
        if (id <= 0)
            return false;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null)
                return false;

            if (job.Status == JobStatusActive)
            {
                _logger.LogWarning("Job with ID {JobId} is already active", id);
                return true;
            }

            job.Status = JobStatusActive;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _schedulerService.ResumeJobAsync(job.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Job resumed successfully with ID {JobId}", job.Id);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to resume job with ID {JobId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<JobExecutionDto>> GetJobHistoryAsync(int jobId)
    {
        if (jobId <= 0)
            return Enumerable.Empty<JobExecutionDto>();

        try
        {
            var executions = await _dbContext.JobExecutions
                .Where(e => e.JobId == jobId)
                .OrderByDescending(e => e.ExecutionTime)
                .Take(100) // Limit to last 100 executions for performance
                .ToListAsync();

            return executions.Select(e => new JobExecutionDto
            {
                Id = e.Id,
                JobId = e.JobId,
                ExecutionTime = e.ExecutionTime,
                Status = e.Status,
                ErrorMessage = e.ErrorMessage,
                Response = !string.IsNullOrEmpty(e.Response) && e.Response.Length > 1000 ? e.Response.Substring(0, 1000) + "..." : e.Response ?? ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve job history for job ID {JobId}", jobId);
            throw;
        }
    }

    private static JobDto MapToDto(Job job)
    {
        return new JobDto
        {
            Id = job.Id,
            Name = job.Name,
            CronExpression = job.CronExpression,
            HttpMethod = job.HttpMethod,
            Url = job.Url,
            Headers = job.Headers,
            Body = job.Body,
            Status = job.Status,
            ExecutionType = job.ExecutionType,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt
        };
    }
}