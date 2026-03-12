using System.Diagnostics;
using Application.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly IQueryOrchestratorService _orchestratorService;
    private readonly IQueryHistoryService _historyService;
    private readonly IDatabaseSchemaService _schemaService;

    public QueryController(
        IQueryOrchestratorService orchestratorService,
        IQueryHistoryService historyService,
        IDatabaseSchemaService schemaService)
    {
        _orchestratorService = orchestratorService;
        _historyService = historyService;
        _schemaService = schemaService;
    }

    [HttpGet("schema")]
    public async Task<IActionResult> GetSchema()
    {
        var schema = await _schemaService.GetDatabaseSchemaJsonAsync();
        return Ok(schema);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var history = await _historyService.GetHistoryAsync();
        return Ok(history);
    }

    public class FeedbackRequest { public string Feedback { get; set; } = string.Empty; }

    [HttpPut("{id}/feedback")]
    public async Task<IActionResult> UpdateFeedback(Guid id, [FromBody] FeedbackRequest req)
    {
        await _historyService.UpdateFeedbackAsync(id, req.Feedback);
        return Ok();
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request)
    {
        var response = await _orchestratorService.AskAsync(request.Prompt);
        
        if (!response.IsSuccess)
        {
            return StatusCode(response.StatusCode, response.ErrorMessage);
        }

        return Ok(response.Data);
    }

    public class ExecuteApprovedRequest
    {
        public QueryIr Ir { get; set; } = new();
        public string OriginalPrompt { get; set; } = "Approved INSERT";
    }

    [HttpPost("execute-approved")]
    public async Task<IActionResult> ExecuteApproved([FromBody] ExecuteApprovedRequest request)
    {
        var response = await _orchestratorService.ExecuteApprovedAsync(request.Ir, request.OriginalPrompt);

        if (!response.IsSuccess)
        {
            return StatusCode(response.StatusCode, response.ErrorMessage);
        }

        return Ok(response.Data);
    }
}