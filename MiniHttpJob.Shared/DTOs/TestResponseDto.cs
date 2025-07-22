namespace MiniHttpJob.Shared.DTOs;

public class TestWebhookResponseDto
{
    public string Message { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = null!;
    public object? ReceivedData { get; set; }
}

public class StatusResponseDto
{
    public string Status { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = null!;
    public string Service { get; set; } = null!;
}