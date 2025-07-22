namespace MiniHttpJob.Admin.Services;

public interface IJobService
{
    Task<JobDto> CreateJobAsync(CreateJobDto createJobDto);
    Task<JobDto?> GetJobAsync(int id);
    Task<IEnumerable<JobDto>> GetJobsAsync();
    Task<JobDto?> UpdateJobAsync(int id, UpdateJobDto updateJobDto);
    Task<bool> DeleteJobAsync(int id);
    Task<bool> PauseJobAsync(int id);
    Task<bool> ResumeJobAsync(int id);
    Task<IEnumerable<JobExecutionDto>> GetJobHistoryAsync(int jobId);
}