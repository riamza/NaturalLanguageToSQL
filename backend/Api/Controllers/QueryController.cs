using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly INaturalLanguageProcessor _nlpProcessor;
    private readonly IValidationEngine _validationEngine;
    private readonly ISqlBuilder _sqlBuilder;
    private readonly IQueryExecutionService _queryExecutionService;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        INaturalLanguageProcessor nlpProcessor,
        IValidationEngine validationEngine,
        ISqlBuilder sqlBuilder,
        IQueryExecutionService queryExecutionService,
        ILogger<QueryController> logger)
    {
        _nlpProcessor = nlpProcessor;
        _validationEngine = validationEngine;
        _sqlBuilder = sqlBuilder;
        _queryExecutionService = queryExecutionService;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Prompt))
            return BadRequest("Prompt cannot be empty");

        try
        {
            // 1. LLM converts NL -> IR
            var ir = await _nlpProcessor.TranslateToIrAsync(request.Prompt);

            // 2. Validation Engine
            var validation = await _validationEngine.ValidateIrAsync(ir);
            if (!validation.IsValid)
            {
                return BadRequest($"Unsafe or invalid intention detected: {validation.ErrorMessage}");
            }

            // 3. SQL Builder
            var (sql, parameters) = _sqlBuilder.BuildSql(ir);
            
            _logger.LogInformation("Executing dynamically generated secure SQL: {Sql}", sql);

            // 4. Execution Service
            var results = await _queryExecutionService.ExecuteQueryAsync(sql, parameters);

            return Ok(new { data = results, generatedSql = sql });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query.");
            return StatusCode(500, "An error occurred while processing the request.");
        }
    }
}

public class QueryRequest
{
    public string Prompt { get; set; } = string.Empty;
}