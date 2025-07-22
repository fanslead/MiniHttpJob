using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MiniHttpJob.Admin.Controllers;
using MiniHttpJob.Admin.Services;
using MiniHttpJob.Shared.DTOs;

namespace MiniHttpJob.Tests;

public class JobControllerTests
{
    private readonly Mock<IJobService> _jobServiceMock;
    private readonly Mock<ILogger<JobController>> _loggerMock;
    private readonly JobController _controller;

    public JobControllerTests()
    {
        _jobServiceMock = new Mock<IJobService>();
        _loggerMock = new Mock<ILogger<JobController>>();
        _controller = new JobController(_jobServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetJobs_ReturnsOkWithJobs()
    {
        // Arrange
        var jobs = new List<JobDto>
        {
            new JobDto { Id = 1, Name = "Job 1", Status = "Active" },
            new JobDto { Id = 2, Name = "Job 2", Status = "Paused" }
        };
        _jobServiceMock.Setup(s => s.GetJobsAsync()).ReturnsAsync(jobs);

        // Act
        var result = await _controller.GetJobs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value;
        Assert.Equal(2, (int)value.count);
    }

    [Fact]
    public async Task GetJob_WithValidId_ReturnsJob()
    {
        // Arrange
        var job = new JobDto { Id = 1, Name = "Test Job", Status = "Active" };
        _jobServiceMock.Setup(s => s.GetJobAsync(1)).ReturnsAsync(job);

        // Act
        var result = await _controller.GetJob(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var jobDto = Assert.IsType<JobDto>(okResult.Value);
        Assert.Equal(1, jobDto.Id);
    }

    [Fact]
    public async Task GetJob_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetJobAsync(999)).ReturnsAsync((JobDto?)null);

        // Act
        var result = await _controller.GetJob(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateJob_WithValidDto_ReturnsCreatedJob()
    {
        // Arrange
        var createDto = new CreateJobDto
        {
            Name = "New Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test"
        };
        var createdJob = new JobDto { Id = 1, Name = "New Job", Status = "Active" };
        _jobServiceMock.Setup(s => s.CreateJobAsync(createDto)).ReturnsAsync(createdJob);

        // Act
        var result = await _controller.CreateJob(createDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var jobDto = Assert.IsType<JobDto>(createdResult.Value);
        Assert.Equal("New Job", jobDto.Name);
    }

    [Fact]
    public async Task UpdateJob_WithValidData_ReturnsUpdatedJob()
    {
        // Arrange
        var updateDto = new UpdateJobDto
        {
            Name = "Updated Job",
            CronExpression = "0/60 * * * * ?",
            HttpMethod = "POST",
            Url = "https://api.example.com/updated"
        };
        var updatedJob = new JobDto { Id = 1, Name = "Updated Job", Status = "Active" };
        _jobServiceMock.Setup(s => s.UpdateJobAsync(1, updateDto)).ReturnsAsync(updatedJob);

        // Act
        var result = await _controller.UpdateJob(1, updateDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var jobDto = Assert.IsType<JobDto>(okResult.Value);
        Assert.Equal("Updated Job", jobDto.Name);
    }

    [Fact]
    public async Task DeleteJob_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.DeleteJobAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteJob(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteJob_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.DeleteJobAsync(999)).ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteJob(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PauseJob_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.PauseJobAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.PauseJob(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ResumeJob_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.ResumeJobAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.ResumeJob(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetJobHistory_WithValidId_ReturnsHistory()
    {
        // Arrange
        var history = new List<JobExecutionDto>
        {
            new JobExecutionDto { Id = 1, JobId = 1, Status = "Success", ExecutionTime = DateTime.UtcNow },
            new JobExecutionDto { Id = 2, JobId = 1, Status = "Failed", ExecutionTime = DateTime.UtcNow.AddMinutes(-5) }
        };
        _jobServiceMock.Setup(s => s.GetJobHistoryAsync(1)).ReturnsAsync(history);

        // Act
        var result = await _controller.GetJobHistory(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value;
        Assert.Equal(2, (int)value.count);
    }
}