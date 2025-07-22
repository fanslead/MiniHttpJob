using DomainValidationException = MiniHttpJob.Shared.Exceptions.ValidationException;

namespace MiniHttpJob.Shared.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            JobNotFoundException => new ApiResponse
            {
                Success = false,
                Message = exception.Message
            },
            InvalidJobConfigurationException => new ApiResponse
            {
                Success = false,
                Message = exception.Message
            },
            DomainValidationException validationEx => new ApiResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = validationEx.ValidationErrors
            },
            _ => new ApiResponse
            {
                Success = false,
                Message = "An error occurred while processing your request"
            }
        };

        context.Response.StatusCode = exception switch
        {
            JobNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidJobConfigurationException => (int)HttpStatusCode.BadRequest,
            DomainValidationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}