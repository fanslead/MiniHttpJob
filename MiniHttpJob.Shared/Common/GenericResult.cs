namespace MiniHttpJob.Shared.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    private Result(bool isSuccess, T? data, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T data) => new(true, data);
    
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
    
    public static Result<T> Failure(List<string> errors)
    {
        var result = new Result<T>(false, default);
        result.Errors = errors;
        return result;
    }
}