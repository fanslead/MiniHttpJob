namespace MiniHttpJob.Admin.Services;

public interface IJobSchedulerService
{
    Task StartAsync();
    Task ScheduleJobAsync(Job job);
    Task UnscheduleJobAsync(int jobId);
    Task PauseJobAsync(int jobId);
    Task ResumeJobAsync(int jobId);
    Task LoadJobsFromDatabaseAsync();
    Task<bool> TriggerJobAsync(int jobId); // 添加手动触发方法
}