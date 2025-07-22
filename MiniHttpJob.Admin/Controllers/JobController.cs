namespace MiniHttpJob.Admin.Controllers;

/// <summary>
/// Job management API controller - supports both versioned and non-versioned endpoints
/// </summary>
[MiniController("api/[controller]")]
public class JobController
{
    private readonly IJobService _jobService;
    private readonly ILogger<JobController> _logger;

    public JobController(IJobService jobService, ILogger<JobController> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new job
    /// </summary>
    /// <param name="createJobDto">Job creation data</param>
    /// <returns>Created job information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<JobDto>, ProblemHttpResult>> CreateJob([FromBody] CreateJobDto createJobDto)
    {
        try
        {
            var job = await _jobService.CreateJobAsync(createJobDto);
                        
            return TypedResults.Ok(job);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when creating job");
            return TypedResults.Problem(ex.Message, title: "Invalid Request", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating job");
            return TypedResults.Problem("An unexpected error occurred while creating the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get all jobs
    /// </summary>
    /// <returns>List of jobs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<IEnumerable<JobDto>>, ProblemHttpResult>> GetJobs()
    {
        try
        {
            var jobs = await _jobService.GetJobsAsync();
            return TypedResults.Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving jobs");
            return TypedResults.Problem("An unexpected error occurred while retrieving jobs", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Job details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<JobDto>, ProblemHttpResult>> GetJob(int id)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var job = await _jobService.GetJobAsync(id);
            if (job == null)
                return TypedResults.Problem($"Job with ID {id} was not found", title: "Job Not Found", statusCode: StatusCodes.Status404NotFound);
            
            return TypedResults.Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving job {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while retrieving the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Update job information
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="updateJobDto">Updated job information</param>
    /// <returns>Updated job information</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<JobDto>, ProblemHttpResult>> UpdateJob(int id, [FromBody] UpdateJobDto updateJobDto)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var job = await _jobService.UpdateJobAsync(id, updateJobDto);
            if (job == null) 
                return TypedResults.Problem($"Job with ID {id} was not found", title: "Job Not Found", statusCode: StatusCodes.Status404NotFound);
            
            return TypedResults.Ok(job);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when updating job {JobId}", id);
            return TypedResults.Problem(ex.Message, title: "Invalid Request", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating job {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while updating the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Delete job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<NoContent, ProblemHttpResult>> DeleteJob(int id)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var result = await _jobService.DeleteJobAsync(id);
            if (!result) 
                return TypedResults.Problem($"Job with ID {id} was not found", title: "Job Not Found", statusCode: StatusCodes.Status404NotFound);
            
            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting job {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while deleting the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Pause job execution
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id:int}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<NoContent, ProblemHttpResult>> PauseJob(int id)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var result = await _jobService.PauseJobAsync(id);
            if (!result) 
                return TypedResults.Problem($"Job with ID {id} was not found", title: "Job Not Found", statusCode: StatusCodes.Status404NotFound);
            
            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error pausing job {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while pausing the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Resume job execution
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id:int}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<NoContent, ProblemHttpResult>> ResumeJob(int id)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var result = await _jobService.ResumeJobAsync(id);
            if (!result) 
                return TypedResults.Problem($"Job with ID {id} was not found", title: "Job Not Found", statusCode: StatusCodes.Status404NotFound);
            
            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resuming job {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while resuming the job", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get job execution history
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Job execution history</returns>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(JobHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<Results<Ok<JobHistoryResponseDto>, ProblemHttpResult>> GetJobHistory(int id)
    {
        if (id <= 0)
            return TypedResults.Problem("Job ID must be a positive integer", title: "Invalid ID", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var history = await _jobService.GetJobHistoryAsync(id);
            var response = new JobHistoryResponseDto { Data = history.ToList(), Count = history.Count() };
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving job history for {JobId}", id);
            return TypedResults.Problem("An unexpected error occurred while retrieving job history", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}