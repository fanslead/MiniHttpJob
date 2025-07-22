namespace MiniHttpJob.Admin.Models;

public class JobExecution
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Success"; // Success, Failed
    public string ErrorMessage { get; set; } = "";
    public string Response { get; set; } = "";
    
    // Navigation property
    public Job? Job { get; set; }
}