namespace MiniHttpJob.Admin.Configuration;

public class EmailOptions
{
    public bool Enabled { get; set; } = false;
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public List<string> ToAddresses { get; set; } = new();
}