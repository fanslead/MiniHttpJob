namespace MiniHttpJob.Shared.Validators;

public class CreateJobDtoValidator : AbstractValidator<CreateJobDto>
{
    public CreateJobDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Job name is required")
            .Length(1, 100).WithMessage("Job name must be between 1 and 100 characters");

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("Cron expression is required")
            .Must(BeValidCronExpression).WithMessage("Invalid cron expression format");

        RuleFor(x => x.HttpMethod)
            .NotEmpty().WithMessage("HTTP method is required")
            .Must(BeValidHttpMethod).WithMessage("Invalid HTTP method");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .Must(BeValidUrl).WithMessage("Invalid URL format");

        RuleFor(x => x.Headers)
            .Must(BeValidJson).WithMessage("Headers must be valid JSON");

        RuleFor(x => x.Body)
            .NotNull().WithMessage("Body cannot be null");
    }

    private static bool BeValidCronExpression(string cronExpression)
    {
        try
        {
            var cron = new CronExpression(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidHttpMethod(string httpMethod)
    {
        var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
        return validMethods.Contains(httpMethod.ToUpper());
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) 
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeValidJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}