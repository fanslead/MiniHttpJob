namespace MiniHttpJob.Shared.Common;

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    private Result(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true);
    
    public static Result Failure(string errorMessage) => new(false, errorMessage);
    
    public static Result Failure(List<string> errors)
    {
        var result = new Result(false);
        result.Errors = errors;
        return result;
    }
}