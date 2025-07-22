namespace MiniHttpJob.Admin.Quartz;

public class SimpleJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SimpleJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var jobType = bundle.JobDetail.JobType;

        // For HttpJob, we need to create it manually to inject the correct dependencies
        if (jobType == typeof(HttpJob))
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<HttpJob>>();
            return new HttpJob(_serviceProvider, logger);
        }

        // For other job types, try to resolve from DI container
        try
        {
            return _serviceProvider.GetRequiredService(jobType) as IJob
                   ?? throw new InvalidOperationException($"Unable to create job of type {jobType}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to create job of type {jobType}: {ex.Message}", ex);
        }
    }

    public void ReturnJob(IJob job)
    {
        // Dispose job if it implements IDisposable
        if (job is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}