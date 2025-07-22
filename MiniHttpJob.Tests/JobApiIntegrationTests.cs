using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using MiniHttpJob.Admin.Data;
using MiniHttpJob.Shared.DTOs;
using MiniHttpJob.Shared.Common;

namespace MiniHttpJob.Tests;

public class JobApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public JobApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<JobDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<JobDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase" + Guid.NewGuid().ToString());
                });

                // Configure logging for tests
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact] 
    public async Task GetJobs_ReturnsSuccessResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/job");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task CreateJob_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var createDto = new CreateJobDto
        {
            Name = "Integration Test Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://httpbin.org/get",
            Headers = "{}",
            Body = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/job", createDto);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task CreateJob_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var invalidDto = new CreateJobDto
        {
            Name = "", // Invalid: empty name
            CronExpression = "invalid-cron",
            HttpMethod = "INVALID",
            Url = "not-a-url"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/job", invalidDto);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}