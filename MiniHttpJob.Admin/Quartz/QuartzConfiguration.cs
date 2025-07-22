namespace MiniHttpJob.Admin.Quartz;

public static class QuartzConfiguration
{
    public static IServiceCollection AddQuartzScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        // Simple configuration for development - use memory storage
        var quartzConfig = new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = "MiniHttpJobScheduler",
            ["quartz.scheduler.instanceId"] = "AUTO",
            ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
            ["quartz.threadPool.threadCount"] = "10"
        };

        var schedulerFactory = new StdSchedulerFactory(quartzConfig);

        services.AddSingleton<ISchedulerFactory>(schedulerFactory);
        services.AddSingleton<IJobFactory, SimpleJobFactory>();
        // Change JobSchedulerService to scoped to allow DbContext dependency
        services.AddScoped<IJobSchedulerService, JobSchedulerService>();
        services.AddTransient<HttpJob>();

        return services;
    }
}