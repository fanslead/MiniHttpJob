using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MiniHttpJob.Admin.Data;
using MiniHttpJob.Admin.Models;
using MiniHttpJob.Admin.Services;
using MiniHttpJob.Shared.DTOs;

namespace MiniHttpJob.Tests;

public class JobServiceTests : IDisposable
{
    private readonly JobDbContext _dbContext;
    private readonly Mock<IJobSchedulerService> _schedulerServiceMock;
    private readonly Mock<ILogger<JobService>> _loggerMock;
    private readonly JobService _jobService;

    public JobServiceTests()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new JobDbContext(options);
        _schedulerServiceMock = new Mock<IJobSchedulerService>();
        _loggerMock = new Mock<ILogger<JobService>>();
        _jobService = new JobService(_dbContext, _schedulerServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateJobAsync_WithValidDto_ReturnsCreatedJob()
    {
        // Arrange
        var createDto = new CreateJobDto
        {
            Name = "Test Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test",
            Headers = "{}",
            Body = ""
        };

        // Act
        var result = await _jobService.CreateJobAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Job", result.Name);
        Assert.Equal("0/30 * * * * ?", result.CronExpression);
        Assert.Equal("GET", result.HttpMethod);
        Assert.Equal("https://api.example.com/test", result.Url);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task CreateJobAsync_WithNullDto_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _jobService.CreateJobAsync(null!));
    }

    [Fact]
    public async Task GetJobsAsync_ReturnsAllJobs()
    {
        // Arrange
        await SeedTestJobsAsync();

        // Act
        var result = await _jobService.GetJobsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task GetJobAsync_WithValidId_ReturnsJob()
    {
        // Arrange
        var job = await CreateTestJobAsync("Test Job");

        // Act
        var result = await _jobService.GetJobAsync(job.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal("Test Job", result.Name);
    }

    [Fact]
    public async Task GetJobAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _jobService.GetJobAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateJobAsync_WithValidData_UpdatesJob()
    {
        // Arrange
        var job = await CreateTestJobAsync("Original Job");
        var updateDto = new UpdateJobDto
        {
            Name = "Updated Job",
            CronExpression = "0/60 * * * * ?",
            HttpMethod = "POST",
            Url = "https://api.example.com/updated",
            Headers = "{\"Content-Type\": \"application/json\"}",
            Body = "{\"updated\": true}"
        };

        // Act
        var result = await _jobService.UpdateJobAsync(job.Id, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Job", result.Name);
        Assert.Equal("0/60 * * * * ?", result.CronExpression);
        Assert.Equal("POST", result.HttpMethod);
    }

    [Fact]
    public async Task UpdateJobAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var updateDto = new UpdateJobDto
        {
            Name = "Updated Job",
            CronExpression = "0/60 * * * * ?",
            HttpMethod = "POST",
            Url = "https://api.example.com/updated"
        };

        // Act
        var result = await _jobService.UpdateJobAsync(999, updateDto);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteJobAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var job = await CreateTestJobAsync("Job to Delete");

        // Act
        var result = await _jobService.DeleteJobAsync(job.Id);

        // Assert
        Assert.True(result);
        
        // Verify job is deleted
        var deletedJob = await _jobService.GetJobAsync(job.Id);
        Assert.Null(deletedJob);
    }

    [Fact]
    public async Task DeleteJobAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _jobService.DeleteJobAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetJobHistoryAsync_ReturnsExecutionHistory()
    {
        // Arrange
        var job = await CreateTestJobAsync("History Test Job");
        await CreateTestExecutionAsync(job.Id, "Success", "");
        await CreateTestExecutionAsync(job.Id, "Failed", "Error message");

        // Act
        var result = await _jobService.GetJobHistoryAsync(job.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    private async Task<Job> CreateTestJobAsync(string name)
    {
        var job = new Job
        {
            Name = name,
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test",
            Headers = "{}",
            Body = "",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();
        return job;
    }

    private async Task SeedTestJobsAsync()
    {
        var jobs = new[]
        {
            new Job { Name = "Job 1", CronExpression = "0/30 * * * * ?", HttpMethod = "GET", 
                     Url = "https://api.example.com/1", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Job { Name = "Job 2", CronExpression = "0/60 * * * * ?", HttpMethod = "POST", 
                     Url = "https://api.example.com/2", Status = "Paused", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Job { Name = "Job 3", CronExpression = "0 0 12 * * ?", HttpMethod = "GET", 
                     Url = "https://api.example.com/3", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _dbContext.Jobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateTestExecutionAsync(int jobId, string status, string errorMessage)
    {
        var execution = new JobExecution
        {
            JobId = jobId,
            ExecutionTime = DateTime.UtcNow,
            Status = status,
            ErrorMessage = errorMessage,
            Response = status == "Success" ? "Success response" : ""
        };

        _dbContext.JobExecutions.Add(execution);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}