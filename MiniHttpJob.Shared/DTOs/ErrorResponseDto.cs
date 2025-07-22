namespace MiniHttpJob.Shared.DTOs;

public class ErrorResponseDto
{
    public string Error { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}