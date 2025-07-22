namespace MiniHttpJob.Admin.Controllers;

[MiniController("api/[controller]")]
public class TestController
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    [HttpGet("webhook")]
    [ProducesResponseType(typeof(TestWebhookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Results<Ok<TestWebhookResponseDto>, ProblemHttpResult> TestWebhook()
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            _logger.LogInformation($"Test webhook called at {timestamp}");

            var response = new TestWebhookResponseDto
            {
                Message = "Webhook received successfully",
                Timestamp = timestamp,
                Source = "MiniHttpJob Test Endpoint"
            };
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test webhook");
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("webhook")]
    [ProducesResponseType(typeof(TestWebhookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Results<Ok<TestWebhookResponseDto>, ProblemHttpResult> TestWebhookPost([FromBody] object? data)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            _logger.LogInformation($"Test webhook POST called at {timestamp} with data: {data}");

            var response = new TestWebhookResponseDto
            {
                Message = "Webhook POST received successfully",
                Timestamp = timestamp,
                ReceivedData = data,
                Source = "MiniHttpJob Test Endpoint"
            };
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test webhook POST");
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(StatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Results<Ok<StatusResponseDto>, ProblemHttpResult> GetStatus()
    {
        try
        {
            var response = new StatusResponseDto
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Service = "MiniHttpJob Scheduler"
            };
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status");
            return TypedResults.Problem("Internal server error", title: "Internal Server Error", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}