using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Models;

namespace Application.Interfaces;

public class OrchestratorResponse<T>
{
    public bool IsSuccess { get; set; } = true;
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; } = 200;

    public static OrchestratorResponse<T> Success(T data) => new() { Data = data };
    public static OrchestratorResponse<T> Failure(string error, int statusCode = 400) => new() { IsSuccess = false, ErrorMessage = error, StatusCode = statusCode };
}

public class AskResult
{
    public bool RequiresApproval { get; set; }
    public string? GeneratedSql { get; set; }
    public QueryIr? Ir { get; set; }
    public string? OriginalPrompt { get; set; }
    public IEnumerable<dynamic>? Data { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Guid? HistoryId { get; set; }
}

public class ExecuteApprovedResult
{
    public bool Success { get; set; }
    public Guid HistoryId { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? GeneratedSql { get; set; }
    public int RowsAffected { get; set; }
}

public interface IQueryOrchestratorService
{
    Task<OrchestratorResponse<AskResult>> AskAsync(string prompt);
    Task<OrchestratorResponse<ExecuteApprovedResult>> ExecuteApprovedAsync(QueryIr ir, string originalPrompt);
}