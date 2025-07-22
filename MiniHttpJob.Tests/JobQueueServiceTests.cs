using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using MiniHttpJob.Worker.Services;
using MiniHttpJob.Shared.SignalR;
using System.Threading.Channels;

namespace MiniHttpJob.Tests;

public class JobQueueServiceTests
{
    private readonly Mock<ILogger<JobQueueService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly JobQueueService _jobQueueService;

    public JobQueueServiceTests()
    {
        _loggerMock = new Mock<ILogger<JobQueueService>>();
        _configurationMock = new Mock<IConfiguration>();
        _jobQueueService = new JobQueueService(_loggerMock.Object, _configurationMock.Object);
    }

    [Fact]
    public async Task EnqueueJobAsync_WithValidCommand_AddsToQueue()
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 1,
            JobName = "Test Job",
            HttpMethod = "GET",
            Url = "https://api.example.com/test",
            Headers = "{}",
            Body = "",
            TimeoutSeconds = 30
        };

        // Act
        await _jobQueueService.EnqueueJobAsync(command);

        // Assert
        var queuedJob = await _jobQueueService.DequeueJobAsync(CancellationToken.None);
        Assert.NotNull(queuedJob);
        Assert.Equal(1, queuedJob.JobId);
        Assert.Equal("Test Job", queuedJob.JobName);
    }

    [Fact]
    public async Task DequeueJobAsync_WithEmptyQueue_WaitsForJob()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _jobQueueService.DequeueJobAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DequeueJobAsync_WithMultipleJobs_ReturnsInFIFOOrder()
    {
        // Arrange
        var command1 = new JobExecutionCommand { JobId = 1, JobName = "Job 1", HttpMethod = "GET", Url = "https://test1.com" };
        var command2 = new JobExecutionCommand { JobId = 2, JobName = "Job 2", HttpMethod = "GET", Url = "https://test2.com" };
        var command3 = new JobExecutionCommand { JobId = 3, JobName = "Job 3", HttpMethod = "GET", Url = "https://test3.com" };

        // Act
        await _jobQueueService.EnqueueJobAsync(command1);
        await _jobQueueService.EnqueueJobAsync(command2);
        await _jobQueueService.EnqueueJobAsync(command3);

        // Assert
        var job1 = await _jobQueueService.DequeueJobAsync(CancellationToken.None);
        var job2 = await _jobQueueService.DequeueJobAsync(CancellationToken.None);
        var job3 = await _jobQueueService.DequeueJobAsync(CancellationToken.None);

        Assert.Equal(1, job1.JobId);
        Assert.Equal(2, job2.JobId);
        Assert.Equal(3, job3.JobId);
    }

    [Fact]
    public void GetQueueLength_ReturnsCorrectCount()
    {
        // Arrange
        Assert.Equal(0, _jobQueueService.GetQueueSizeAsync().Result);

        // Act
        _jobQueueService.EnqueueJobAsync(new JobExecutionCommand { JobId = 1, JobName = "Job 1", HttpMethod = "GET", Url = "https://test1.com" });
        _jobQueueService.EnqueueJobAsync(new JobExecutionCommand { JobId = 2, JobName = "Job 2", HttpMethod = "GET", Url = "https://test2.com" });

        // Assert
        Assert.Equal(2, _jobQueueService.GetQueueSizeAsync().Result);
    }

    [Fact]
    public async Task ConcurrentOperations_HandlesProperly()
    {
        // Arrange
        var tasks = new List<Task>();
        const int concurrentJobs = 10;

        // Act - Enqueue jobs concurrently
        for (int i = 0; i < concurrentJobs; i++)
        {
            int jobId = i + 1;
            var command = new JobExecutionCommand 
            { 
                JobId = jobId, 
                JobName = $"Concurrent Job {jobId}", 
                HttpMethod = "GET", 
                Url = $"https://test{jobId}.com" 
            };
            tasks.Add(_jobQueueService.EnqueueJobAsync(command));
        }

        await Task.WhenAll(tasks);

        // Assert - Dequeue all jobs
        var dequeuedJobs = new List<JobExecutionCommand>();
        for (int i = 0; i < concurrentJobs; i++)
        {
            var job = await _jobQueueService.DequeueJobAsync(CancellationToken.None);
            dequeuedJobs.Add(job);
        }

        Assert.Equal(concurrentJobs, dequeuedJobs.Count);
        Assert.Equal(0, _jobQueueService.GetQueueSizeAsync().Result);
    }
}