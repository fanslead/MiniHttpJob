namespace MiniHttpJob.Shared.DTOs;

public class HealthCheckDto
{
    public string Status { get; set; } = null!;
    public double TotalDuration { get; set; }
    public List<HealthCheckEntryDto> Checks { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class HealthCheckEntryDto
{
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Description { get; set; }
    public double Duration { get; set; }
    public IReadOnlyDictionary<string, object>? Data { get; set; }
    public string? Exception { get; set; }
}