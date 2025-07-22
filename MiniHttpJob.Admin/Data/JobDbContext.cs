namespace MiniHttpJob.Admin.Data;

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options) : base(options) { }

    public DbSet<Job> Jobs { get; set; } = null!;
    public DbSet<JobExecution> JobExecutions { get; set; } = null!;
}