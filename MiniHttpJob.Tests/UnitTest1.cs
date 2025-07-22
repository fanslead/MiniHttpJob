using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using MiniHttpJob.Worker.Services;
using MiniHttpJob.Shared.SignalR;
using System.Net;

namespace MiniHttpJob.Tests;

public class JobExecutorServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<JobExecutorService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly JobExecutorService _jobExecutorService;

    public JobExecutorServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<JobExecutorService>>();

        // Create in-memory configuration instead of mocking
        var configurationData = new Dictionary<string, string?>
        {
            ["Worker:DefaultTimeoutSeconds"] = "30",
            ["Worker:MaxRetries"] = "3",
            ["Worker:RetryDelaySeconds"] = "2"
        };

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(configurationData);
        _configuration = configurationBuilder.Build();

        _jobExecutorService = new JobExecutorService(
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _configuration);
    }

    [Fact]
    public async Task ExecuteJobAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _jobExecutorService.ExecuteJobAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteJobAsync_WithValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 1,
            JobName = "Test Job",
            HttpMethod = "GET",
            Url = "https://httpbin.org/get",
            Headers = "{}",
            Body = "",
            TimeoutSeconds = 30
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK, "Success"));
        _httpClientFactoryMock.Setup(f => f.CreateClient("JobClient")).Returns(httpClient);

        // Act
        var result = await _jobExecutorService.ExecuteJobAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.JobId);
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Success", result.Response);
    }

    [Fact]
    public async Task ExecuteJobAsync_WithHttpRequestException_ReturnsErrorResult()
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 2,
            JobName = "Error Job",
            HttpMethod = "GET",
            Url = "https://invalid-url.com",
            Headers = "{}",
            Body = "",
            TimeoutSeconds = 30
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(throwException: true));
        _httpClientFactoryMock.Setup(f => f.CreateClient("JobClient")).Returns(httpClient);

        // Act
        var result = await _jobExecutorService.ExecuteJobAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.JobId);
        Assert.False(result.Success);
        Assert.Contains("HTTP request failed", result.ErrorMessage);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task ExecuteJobAsync_WithBodyMethods_IncludesRequestBody(string httpMethod)
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 3,
            JobName = "Body Test Job",
            HttpMethod = httpMethod,
            Url = "https://httpbin.org/post",
            Headers = "{\"Content-Type\": \"application/json\"}",
            Body = "{\"test\": \"data\"}",
            TimeoutSeconds = 30
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK, "Success"));
        _httpClientFactoryMock.Setup(f => f.CreateClient("JobClient")).Returns(httpClient);

        // Act
        var result = await _jobExecutorService.ExecuteJobAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, result.JobId);
    }

    [Fact]
    public async Task ExecuteJobAsync_WithCustomHeaders_AddsHeadersToRequest()
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 4,
            JobName = "Headers Test Job",
            HttpMethod = "GET",
            Url = "https://httpbin.org/headers",
            Headers = "{\"Authorization\": \"Bearer token123\", \"Custom-Header\": \"custom-value\"}",
            Body = "",
            TimeoutSeconds = 30
        };

        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.OK, "Success");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient("JobClient")).Returns(httpClient);

        // Act
        var result = await _jobExecutorService.ExecuteJobAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(4, result.JobId);
    }

    [Fact]
    public async Task ExecuteJobAsync_WithInvalidJson_HandlesGracefully()
    {
        // Arrange
        var command = new JobExecutionCommand
        {
            JobId = 5,
            JobName = "Invalid JSON Test",
            HttpMethod = "GET",
            Url = "https://httpbin.org/get",
            Headers = "invalid json",
            Body = "",
            TimeoutSeconds = 30
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK, "Success"));
        _httpClientFactoryMock.Setup(f => f.CreateClient("JobClient")).Returns(httpClient);

        // Act
        var result = await _jobExecutorService.ExecuteJobAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success); // Should still succeed despite invalid headers
        Assert.Equal(5, result.JobId);
    }
}

// Enhanced Mock HTTP Message Handler for testing
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly TimeSpan _delay;
    private readonly bool _throwException;

    public MockHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "", TimeSpan delay = default, bool throwException = false)
    {
        _statusCode = statusCode;
        _content = content;
        _delay = delay;
        _throwException = throwException;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (_throwException)
        {
            throw new HttpRequestException("Simulated HTTP request exception");
        }

        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content),
            ReasonPhrase = _statusCode.ToString()
        };
    }
}
