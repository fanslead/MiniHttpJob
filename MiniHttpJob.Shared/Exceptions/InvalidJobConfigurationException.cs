namespace MiniHttpJob.Shared.Exceptions;

public class InvalidJobConfigurationException : DomainException
{
    public InvalidJobConfigurationException(string message) : base(message) { }
}