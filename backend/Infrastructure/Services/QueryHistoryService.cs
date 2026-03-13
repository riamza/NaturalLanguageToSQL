using Application.Interfaces;
using Core.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class QueryHistoryService : IQueryHistoryService
{
    private readonly ApplicationDbContext _dbContext;

    public QueryHistoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> LogQueryAsync(string prompt, string? generatedSql, long executionTimeMs, bool isSuccessful, string? errorMessage)
    {
        var history = new QueryHistory
        {
            Id = Guid.NewGuid(),
            Prompt = prompt,
            GeneratedSql = generatedSql,
            ExecutionTimeMs = executionTimeMs,
            IsSuccessful = isSuccessful,
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.QueryHistories.Add(history);
        await _dbContext.SaveChangesAsync();

        return history.Id;
    }

    public async Task UpdateFeedbackAsync(Guid id, string feedback)
    {
        var history = await _dbContext.QueryHistories.FindAsync(id);
        if (history != null)
        {
            history.UserFeedback = feedback;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<QueryHistory>> GetHistoryAsync()
    {
        return await _dbContext.QueryHistories
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<QueryHistory?> GetCachedSuccessfulQueryAsync(string prompt)
    {
        var standardizedPrompt = prompt.Trim().ToLowerInvariant();
        return await _dbContext.QueryHistories
            .Where(q => q.IsSuccessful && q.Prompt.Trim().ToLower() == standardizedPrompt && !string.IsNullOrEmpty(q.GeneratedSql))
            .OrderByDescending(q => q.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }
}