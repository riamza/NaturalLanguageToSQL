using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class QueryOrchestratorService : IQueryOrchestratorService
{
    private readonly INaturalLanguageProcessor _nlpProcessor;
    private readonly IValidationEngine _validationEngine;
    private readonly ISqlBuilder _sqlBuilder;
    private readonly IQueryExecutionService _queryExecutionService;
    private readonly IQueryHistoryService _historyService;
    private readonly IDatabaseSchemaService _schemaService;
    private readonly ILogger<QueryOrchestratorService> _logger;

    public QueryOrchestratorService(
        INaturalLanguageProcessor nlpProcessor,
        IValidationEngine validationEngine,
        ISqlBuilder sqlBuilder,
        IQueryExecutionService queryExecutionService,
        IQueryHistoryService historyService,
        IDatabaseSchemaService schemaService,
        ILogger<QueryOrchestratorService> logger)
    {
        _nlpProcessor = nlpProcessor;
        _validationEngine = validationEngine;
        _sqlBuilder = sqlBuilder;
        _queryExecutionService = queryExecutionService;
        _historyService = historyService;
        _schemaService = schemaService;
        _logger = logger;
    }

    public async Task<OrchestratorResponse<AskResult>> AskAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return OrchestratorResponse<AskResult>.Failure("Prompt cannot be empty");

        var stopwatch = Stopwatch.StartNew();
        string? currentSqlBase = null;

        try
        {
            var cachedQuery = await _historyService.GetCachedSuccessfulQueryAsync(prompt);
            if (cachedQuery != null && !string.IsNullOrEmpty(cachedQuery.GeneratedSql))
            {
                _logger.LogInformation("Cache hit for prompt: '{Prompt}' -> Executing SQL: {Sql}", prompt, cachedQuery.GeneratedSql);
                
                // Dacă este din cache, bypassăm LLM și folosim direct SQL-ul precedent
                var results = await _queryExecutionService.ExecuteQueryAsync(cachedQuery.GeneratedSql, new Dictionary<string, object>());
                stopwatch.Stop();
                
                var historyId = await _historyService.LogQueryAsync(prompt, cachedQuery.GeneratedSql, stopwatch.ElapsedMilliseconds, true, null);
                
                return OrchestratorResponse<AskResult>.Success(new AskResult
                {
                    Data = results,
                    GeneratedSql = cachedQuery.GeneratedSql,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    HistoryId = historyId
                });
            }

            var ir = await _nlpProcessor.TranslateToIrAsync(prompt);

            if (ir.Action == "ERROR")
            {
                return await HandleIrErrorAsync(prompt, ir, stopwatch);
            }

            var validation = await _validationEngine.ValidateIrAsync(ir);
            if (!validation.IsValid)
            {
                return await HandleValidationErrorAsync(prompt, validation.ErrorMessage, stopwatch);
            }

            var (sql, parameters) = _sqlBuilder.BuildSql(ir);
            currentSqlBase = sql;
            _logger.LogInformation("Executing dynamically generated secure SQL: {Sql}", sql);

            if (ir.Action != "SELECT")
            {
                return await ProcessNonSelectQueryAsync(prompt, ir, sql, parameters, stopwatch);
            }

            return await ExecuteSelectQueryAsync(prompt, sql, parameters, stopwatch);
        }
        catch (Exception ex)
        {
            return await HandleExecutionErrorAsync<AskResult>(prompt, currentSqlBase, ex, stopwatch);
        }
    }

    public async Task<OrchestratorResponse<ExecuteApprovedResult>> ExecuteApprovedAsync(QueryIr ir, string originalPrompt)
    {
        var stopwatch = Stopwatch.StartNew();
        string currentSqlBase = "Unknown SQL (Failed to build)";
        
        try
        {
            var (sql, parameters) = _sqlBuilder.BuildSql(ir);
            currentSqlBase = GetReadableSql(sql, parameters);

            var results = await _queryExecutionService.ExecuteQueryAsync(sql, parameters);
            stopwatch.Stop();

            var historyId = await _historyService.LogQueryAsync(originalPrompt, currentSqlBase, stopwatch.ElapsedMilliseconds, true, null);
            return OrchestratorResponse<ExecuteApprovedResult>.Success(new ExecuteApprovedResult
            {
                Success = true,
                HistoryId = historyId,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                GeneratedSql = currentSqlBase,
                RowsAffected = results.Count()
            });
        }
        catch (Exception ex)
        {
            return await HandleExecutionErrorAsync<ExecuteApprovedResult>(originalPrompt, currentSqlBase, ex, stopwatch);
        }
    }

    #region Private Helper Methods

    private string GetReadableSql(string sql, Dictionary<string, object> parameters)
    {
        var readableSql = sql;
        foreach (var param in parameters.OrderByDescending(p => p.Key.Length))
        {
            var valStr = param.Value == null ? "NULL" : 
                         (param.Value is string || param.Value is DateTime) ? $"'{param.Value}'" : 
                         param.Value.ToString();
            readableSql = readableSql.Replace(param.Key, valStr);
        }
        return readableSql;
    }

    private async Task<OrchestratorResponse<AskResult>> HandleIrErrorAsync(string prompt, QueryIr ir, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        var errorMsg = string.IsNullOrWhiteSpace(ir.ErrorDetails) ? "Nu am putut genera un query valid din textul furnizat." : ir.ErrorDetails;
        await _historyService.LogQueryAsync(prompt, null, stopwatch.ElapsedMilliseconds, false, errorMsg);
        return OrchestratorResponse<AskResult>.Failure(errorMsg);
    }

    private async Task<OrchestratorResponse<AskResult>> HandleValidationErrorAsync(string prompt, string errorMessage, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        await _historyService.LogQueryAsync(prompt, null, stopwatch.ElapsedMilliseconds, false, errorMessage);
        return OrchestratorResponse<AskResult>.Failure($"Unsafe or invalid intention detected: {errorMessage}");
    }

    private async Task<OrchestratorResponse<AskResult>> ProcessNonSelectQueryAsync(string prompt, QueryIr ir, string sql, Dictionary<string, object> parameters, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return OrchestratorResponse<AskResult>.Success(new AskResult
        {
            RequiresApproval = true,
            GeneratedSql = GetReadableSql(sql, parameters),
            Ir = ir,
            OriginalPrompt = prompt
        });
    }

    private async Task<OrchestratorResponse<AskResult>> ExecuteSelectQueryAsync(string prompt, string sql, Dictionary<string, object> parameters, Stopwatch stopwatch)
    {
        var results = await _queryExecutionService.ExecuteQueryAsync(sql, parameters);
        stopwatch.Stop();

        var readableSql = GetReadableSql(sql, parameters);

        var historyId = await _historyService.LogQueryAsync(prompt, readableSql, stopwatch.ElapsedMilliseconds, true, null);

        return OrchestratorResponse<AskResult>.Success(new AskResult
        {
            Data = results,
            GeneratedSql = readableSql,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            HistoryId = historyId
        });
    }

    private async Task<OrchestratorResponse<T>> HandleExecutionErrorAsync<T>(string prompt, string? currentSqlBase, Exception ex, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        _logger.LogError(ex, "Error processing query.");

        var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        
        if (!errorMsg.Contains("LLM error") && !errorMsg.Contains("TooManyRequests"))
        {
            try
            {
                var schemaContext = await _schemaService.GetDatabaseSchemaDescriptionAsync();
                var aiSuggestion = await _nlpProcessor.GetErrorSuggestionAsync(errorMsg, prompt, schemaContext);
                errorMsg = $"Eroare SQL: {errorMsg}\n\n**Sugestie AI:** {aiSuggestion}";
            }
            catch (Exception aiEx)
            {
                _logger.LogWarning(aiEx, "Failed to get AI suggestion for error.");
            }
        }
        else
        {
            errorMsg = $"Eroare LLM API: {errorMsg}";
        }

        await _historyService.LogQueryAsync(prompt, currentSqlBase, stopwatch.ElapsedMilliseconds, false, errorMsg);

        return OrchestratorResponse<T>.Failure(errorMsg, 500);
    }

    #endregion
}