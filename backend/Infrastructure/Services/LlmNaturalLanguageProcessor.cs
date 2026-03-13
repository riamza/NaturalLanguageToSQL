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
    private readonly IDatabaseSchemaService _schemaService;
    private readonly string _modelUrl;
    private readonly string _modelName;
    private readonly ILogger<LlmNaturalLanguageProcessor> _logger;
    private readonly Dictionary<string, string> _prompts = new();

    public LlmNaturalLanguageProcessor(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<LlmNaturalLanguageProcessor> logger,
        IDatabaseSchemaService schemaService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _schemaService = schemaService;

        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? configuration["Llm:ApiKey"] ?? string.Empty;
        _modelUrl = configuration["Llm:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        _modelName = configuration["Llm:ModelName"] ?? "gpt-3.5-turbo";

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        LoadPrompts();
    }

    private void LoadPrompts()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var promptPath = Path.Combine(basePath, "Resources", "system_prompt.txt");
        if (!File.Exists(promptPath))
        {
            var projectRoot = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "";
            promptPath = Path.Combine(projectRoot, "Infrastructure", "Resources", "system_prompt.txt");
        }

        if (File.Exists(promptPath))
        {
            var lines = File.ReadAllLines(promptPath);
            string? currentKey = null;
            var currentContent = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("=== ") && line.EndsWith(" ==="))
                {
                    if (currentKey != null)
                    {
                        _prompts[currentKey] = currentContent.ToString().Trim();
                        currentContent.Clear();
                    }
                    currentKey = line.Replace("===", "").Trim();
                }
                else if (currentKey != null)
                {
                    currentContent.AppendLine(line);
                }
            }
            if (currentKey != null)
            {
                _prompts[currentKey] = currentContent.ToString().Trim();
            }
        }
        else
        {
            _logger.LogWarning($"Could not find system prompt file at {promptPath}. LLM will use empty prompts.");
        }
    }

    public async Task<QueryIr> TranslateToIrAsync(string naturalLanguageQuery)
    {
        var databaseSchemaContext = await _schemaService.GetDatabaseSchemaDescriptionAsync();
        var systemPromptTemplate = _prompts.GetValueOrDefault("TRANSLATE_SYSTEM_PROMPT", "");
        var systemPrompt = systemPromptTemplate.Replace("{{SCHEMA}}", databaseSchemaContext);

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
            throw new Exception($"Failed to translate natural language to IR due to an LLM error: {error}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        return ParseLlmResponse(responseString);
    }



    private QueryIr ParseLlmResponse(string responseString)
    {
        try 
        {
            using var jsonDocument = JsonDocument.Parse(responseString);
            var resultJson = jsonDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (resultJson != null && resultJson.StartsWith("```json"))
            {
                resultJson = resultJson.Replace("```json", "").Replace("```", "").Trim();
            }

            var serializeOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (resultJson != null && resultJson.TrimStart().StartsWith("["))
            {
                var irList = JsonSerializer.Deserialize<List<QueryIr>>(resultJson, serializeOptions);
                return irList?.FirstOrDefault() ?? new QueryIr();
            }
            
            if (resultJson != null && resultJson.TrimEnd().EndsWith("}") && (resultJson.Contains("}\n{") || resultJson.Contains("}{")))
            {
                var fixedJson = "[" + resultJson.Replace("}\n{", "},{").Replace("}{", "},{") + "]";
                var irList = JsonSerializer.Deserialize<List<QueryIr>>(fixedJson, serializeOptions);
                return irList?.FirstOrDefault() ?? new QueryIr();
            }

            var ir = JsonSerializer.Deserialize<QueryIr>(resultJson ?? "{}", serializeOptions);
            return ir ?? new QueryIr();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from LLM.");
            return new QueryIr { Action = "ERROR", ErrorDetails = "Eroare la parsarea raspunsului de la asistentul AI." };
        }
    }

    public async Task<string> GetErrorSuggestionAsync(string errorMessage, string userQuery, string schemaContext)
    {
        var sysTemplate = _prompts.GetValueOrDefault("ERROR_SUGGESTION_SYSTEM_PROMPT", "");
        var systemPrompt = sysTemplate.Replace("{{SCHEMA}}", schemaContext);

        var userTemplate = _prompts.GetValueOrDefault("ERROR_SUGGESTION_USER_PROMPT", "");
        var userPrompt = userTemplate.Replace("{{USER_QUERY}}", userQuery).Replace("{{ERROR_MESSAGE}}", errorMessage);

        var requestBody = new
        {
            model = _modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_modelUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                return "O eroare SQL a avut loc, dar nu am putut genera o sugestie detaliată.";
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);
            var resultJson = jsonDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return resultJson?.Trim() ?? "O eroare SQL a avut loc, dar nu am putut genera o sugestie detaliată.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error suggestion from LLM.");
            return "O eroare SQL a avut loc, iar asistentul nu a putut fi contactat pentru sugestii.";
        }
    }
}



