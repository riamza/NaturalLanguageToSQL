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
    private readonly ILogger<QueryOrchestratorService> _logger;

    public QueryOrchestratorService(
        INaturalLanguageProcessor nlpProcessor,
        IValidationEngine validationEngine,
        ISqlBuilder sqlBuilder,
        IQueryExecutionService queryExecutionService,
        IQueryHistoryService historyService,
        ILogger<QueryOrchestratorService> logger)
    {
        _nlpProcessor = nlpProcessor;
        _validationEngine = validationEngine;
        _sqlBuilder = sqlBuilder;
        _queryExecutionService = queryExecutionService;
        _historyService = historyService;
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
            var ir = await _nlpProcessor.TranslateToIrAsync(prompt);

            if (ir.Action == "ERROR")
            {
                stopwatch.Stop();
                var errorMsg = string.IsNullOrWhiteSpace(ir.ErrorDetails) ? "Nu am putut genera un query valid din textul furnizat." : ir.ErrorDetails;
                await _historyService.LogQueryAsync(prompt, null, stopwatch.ElapsedMilliseconds, false, errorMsg);
                return OrchestratorResponse<AskResult>.Failure(errorMsg);
            }

            var validation = await _validationEngine.ValidateIrAsync(ir);
            if (!validation.IsValid)
            {
                stopwatch.Stop();
                await _historyService.LogQueryAsync(prompt, null, stopwatch.ElapsedMilliseconds, false, validation.ErrorMessage);
                return OrchestratorResponse<AskResult>.Failure($"Unsafe or invalid intention detected: {validation.ErrorMessage}");
            }

            var (sql, parameters) = _sqlBuilder.BuildSql(ir);
            currentSqlBase = sql;
            _logger.LogInformation("Executing dynamically generated secure SQL: {Sql}", sql);

            if (ir.Action != "SELECT")
            {
                stopwatch.Stop();

                var readableSql = sql;
                foreach (var param in parameters.OrderByDescending(p => p.Key.Length))
                {
                    var valStr = param.Value == null ? "NULL" : 
                                 (param.Value is string || param.Value is DateTime) ? $"'{param.Value}'" : 
                                 param.Value.ToString();
                    readableSql = readableSql.Replace(param.Key, valStr);
                }

                return OrchestratorResponse<AskResult>.Success(new AskResult
                {
                    RequiresApproval = true,
                    GeneratedSql = readableSql,
                    Ir = ir,
                    OriginalPrompt = prompt
                });
            }

            var results = await _queryExecutionService.ExecuteQueryAsync(sql, parameters);
            stopwatch.Stop();

            var historyId = await _historyService.LogQueryAsync(prompt, sql, stopwatch.ElapsedMilliseconds, true, null);

            return OrchestratorResponse<AskResult>.Success(new AskResult
            {
                Data = results,
                GeneratedSql = sql,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                HistoryId = historyId
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing query.");

            var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            
            if (!ex.Message.StartsWith("LLM Error"))
            {
                var aiSuggestion = await _nlpProcessor.GetErrorSuggestionAsync(errorMsg, prompt);
                errorMsg = $"Eroare SQL: {errorMsg}\n\n**Sugestie AI:** {aiSuggestion}";
            }
            else
            {
                errorMsg = ex.Message;
            }

            await _historyService.LogQueryAsync(prompt, currentSqlBase, stopwatch.ElapsedMilliseconds, false, errorMsg);

            return OrchestratorResponse<AskResult>.Failure(errorMsg);
        }
    }

    public async Task<OrchestratorResponse<ExecuteApprovedResult>> ExecuteApprovedAsync(QueryIr ir, string originalPrompt)
    {
        var stopwatch = Stopwatch.StartNew();
        string currentSqlBase = "Unknown SQL (Failed to build)";
        try
        {
            var (sql, parameters) = _sqlBuilder.BuildSql(ir);

            var readableSql = sql;
            foreach (var param in parameters.OrderByDescending(p => p.Key.Length))
            {
                var valStr = param.Value == null ? "NULL" :
                             (param.Value is string || param.Value is DateTime) ? $"'{param.Value}'" :
                             param.Value.ToString();
                readableSql = readableSql.Replace(param.Key, valStr);
            }
            currentSqlBase = readableSql;

            var results = await _queryExecutionService.ExecuteQueryAsync(sql, parameters);
            stopwatch.Stop();

            var historyId = await _historyService.LogQueryAsync(originalPrompt, readableSql, stopwatch.ElapsedMilliseconds, true, null);
            return OrchestratorResponse<ExecuteApprovedResult>.Success(new ExecuteApprovedResult
            {
                Success = true,
                HistoryId = historyId,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                GeneratedSql = readableSql
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

            var aiSuggestion = await _nlpProcessor.GetErrorSuggestionAsync(errorMsg, originalPrompt);
            errorMsg = $"Eroare SQL: {errorMsg}\n\n**Sugestie AI:** {aiSuggestion}";

            await _historyService.LogQueryAsync(originalPrompt, currentSqlBase, stopwatch.ElapsedMilliseconds, false, errorMsg);

            return OrchestratorResponse<ExecuteApprovedResult>.Failure(errorMsg, 500);
        }
    }
}