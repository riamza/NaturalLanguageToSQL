using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class LlmNaturalLanguageProcessor : INaturalLanguageProcessor
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelUrl;
    private readonly string _modelName;
    private readonly ILogger<LlmNaturalLanguageProcessor> _logger;

    public LlmNaturalLanguageProcessor(HttpClient httpClient, IConfiguration configuration, ILogger<LlmNaturalLanguageProcessor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _apiKey = configuration["Llm:ApiKey"] ?? string.Empty;
        _modelUrl = configuration["Llm:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        _modelName = configuration["Llm:ModelName"] ?? "gpt-3.5-turbo";
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<QueryIr> TranslateToIrAsync(string naturalLanguageQuery)
    {
        var systemPrompt = @"
You are an AI that translates natural language questions into an Intermediate Representation (IR) JSON for querying a PostgreSQL database.
The available table is currently: 'employees' with columns: 'Id', 'FirstName', 'LastName', 'Department', 'Salary', 'HireDate'.

RULES:
1. Output ONLY valid JSON matching exactly this C# class structure. 
2. No markdown formatting, no code blocks, no explanations. Just raw JSON.
3. Use exact column names with correct casing from the table definition. 
4. SelectColumns should be specific unless the user asks for 'all' (use ['*']).

JSON Structure Example:
{
  ""Table"": ""employees"",
  ""SelectColumns"": [""FirstName"", ""Salary""],
  ""WhereClauses"": [
    {
      ""Column"": ""Department"",
      ""Operator"": ""="",
      ""Value"": ""IT""
    },
    {
      ""Column"": ""Salary"",
      ""Operator"": "">"",
      ""Value"": 50000
    }
  ],
  ""OrderClauses"": [
    {
      ""Column"": ""Salary"",
      ""Direction"": ""DESC""
    }
  ],
  ""Limit"": 10
}";

        var payload = new
        {
            model = _modelName,
            temperature = 0,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = naturalLanguageQuery }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_modelUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("LLM API failed with status code {StatusCode}. Exception: {Error}", response.StatusCode, error);
            throw new Exception("Failed to translate natural language to IR due to an LLM error.");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        
        // Deserialize OpenAI response structure
        using var jsonDocument = JsonDocument.Parse(responseString);
        var resultJson = jsonDocument.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        // Clean up markdown in case the model failed to follow rule 2
        if (resultJson != null && resultJson.StartsWith("```json"))
        {
            resultJson = resultJson.Replace("```json", "").Replace("```", "").Trim();
        }

        var serializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var ir = JsonSerializer.Deserialize<QueryIr>(resultJson ?? "{}", serializeOptions);

        return ir ?? new QueryIr();
    }
}