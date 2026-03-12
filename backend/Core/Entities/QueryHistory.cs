namespace Core.Entities;

public class QueryHistory
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? GeneratedSql { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserFeedback { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}