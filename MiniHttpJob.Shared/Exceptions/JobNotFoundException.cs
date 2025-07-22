namespace MiniHttpJob.Shared.Exceptions;

public class JobNotFoundException : DomainException
{
    public JobNotFoundException(int jobId) : base($"Job with ID {jobId} was not found.") { }
}