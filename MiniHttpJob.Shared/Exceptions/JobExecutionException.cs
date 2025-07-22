namespace MiniHttpJob.Shared.Exceptions;

public class JobExecutionException : DomainException
{
    public JobExecutionException(string message) : base(message) { }
    public JobExecutionException(string message, Exception innerException) : base(message, innerException) { }
}