using Core.Entities;

namespace Application.Interfaces;

public interface IQueryHistoryService
{
    Task<Guid> LogQueryAsync(string prompt, string? generatedSql, long executionTimeMs, bool isSuccessful, string? errorMessage);
    Task UpdateFeedbackAsync(Guid id, string feedback);
    Task<List<QueryHistory>> GetHistoryAsync();
}