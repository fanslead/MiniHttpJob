namespace MiniHttpJob.Shared.Exceptions;

public class ValidationException : DomainException
{
    public List<string> ValidationErrors { get; }

    public ValidationException(List<string> errors) : base("Validation failed")
    {
        ValidationErrors = errors;
    }
}