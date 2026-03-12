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
    }

    public async Task<QueryIr> TranslateToIrAsync(string naturalLanguageQuery)
    {
        var databaseSchemaContext = await _schemaService.GetDatabaseSchemaDescriptionAsync();

        var systemPrompt = @"You are an AI that translates natural language questions into an Intermediate Representation (IR) JSON for querying a PostgreSQL database.

The available database schema is strictly the following:
" + databaseSchemaContext + @"

RULES:
1. Output ONLY valid JSON matching exactly this C# class structure.
2. No markdown formatting, no code blocks, no explanations. Just raw JSON.
3. Use exact table names and column names with correct casing from the schema definition provided above.
4. If the user asks to query/read data, use ""Action"": ""SELECT"". SelectColumns should be specific unless the user asks for 'all' (use ['*']).
5. ALWAYS defaults to ""UPSERT"" instead of ""INSERT"" when inserting or adding data. You MUST specify ""ConflictColumns"" (usually the primary key like ""Id"") to handle duplicates. If the user asks to update specific rows, use ""Action"": ""UPDATE"" and use ""SetClauses"" to specify the columns to change and ""WhereClauses"" to target the rows. If the user asks to delete rows, use ""Action"": ""DELETE"" and use ""WhereClauses"".
6. If the user asks to create a table, use ""Action"": ""CREATE_TABLE"". Define ""TableColumns"" containing objects with ""Name"" (string), ""DataType"" (string like SERIAL, INTEGER, VARCHAR(255), TIMESTAMP), ""IsPrimaryKey"" (boolean), and ""IsNullable"" (boolean). If a column is a foreign key, also define ""ReferencesTable"" (string) and ""ReferencesColumn"" (string).
7. If the user query is non-sensical, random text, impossible to understand, or cannot be resolved to the DB schema, use ""Action"": ""ERROR"" and populate ""ErrorDetails"" with a message in Romanian explaining the issue.
  8. MUST RETURN EXACTLY ONE JSON OBJECT. Do NOT return an array of objects. If the user requests multiple actions (e.g. creating two tables), process ONLY the first one and ignore the second.
{
  ""Action"": ""CREATE_TABLE"",
  ""Table"": ""new_table_name"",
  ""TableColumns"": [
    { ""Name"": ""id"", ""DataType"": ""SERIAL"", ""IsPrimaryKey"": true, ""IsNullable"": false },
    { ""Name"": ""name"", ""DataType"": ""VARCHAR(100)"", ""IsPrimaryKey"": false, ""IsNullable"": false },
    { ""Name"": ""user_id"", ""DataType"": ""INTEGER"", ""IsPrimaryKey"": false, ""IsNullable"": true, ""ReferencesTable"": ""users"", ""ReferencesColumn"": ""id"" }
  ]
}

JSON Structure Example (ERROR):
{
  ""Action"": ""ERROR"",
  ""ErrorDetails"": ""Nu pot genera un query din textul introdus, deoarece pare a fi un text aleator sau nu se refera la datele disponibile.""
}

JSON Structure Example (SELECT):
{
  ""Action"": ""SELECT"",
  ""Table"": ""employees"",
  ""SelectColumns"": [""FirstName"", ""Salary""],
  ""WhereClauses"": [
    {
      ""Column"": ""Department"",
      ""Operator"": ""="",
      ""Value"": ""IT""
    }
  ],
  ""OrderClauses"": [
    {
      ""Column"": ""Salary"",
      ""Direction"": ""DESC""
    }
  ],
  ""Limit"": 10
}

JSON Structure Example (UPDATE):
{
  ""Action"": ""UPDATE"",
  ""Table"": ""employees"",
  ""SetClauses"": [
    { ""Column"": ""Salary"", ""Value"": 75000 }
  ],
  ""WhereClauses"": [
    {
      ""Column"": ""FirstName"",
      ""Operator"": ""="",
      ""Value"": ""John""
    }
  ]
}

JSON Structure Example (DELETE):
{
  ""Action"": ""DELETE"",
  ""Table"": ""employees"",
  ""WhereClauses"": [
    {
      ""Column"": ""Department"",
      ""Operator"": ""="",
      ""Value"": ""HR""
    }
  ]
}

JSON Structure Example (UPSERT):
{
  ""Action"": ""UPSERT"",
  ""Table"": ""employees"",
  ""InsertColumns"": [""Id"", ""FirstName"", ""LastName"", ""Department"", ""Salary"", ""HireDate""],
  ""InsertValues"": [
    [1, ""John"", ""Doe"", ""IT"", 65000, ""2021-05-10""]
  ],
  ""ConflictColumns"": [""Id""]
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

            // Detect if the LLM returned an array of objects
            if (resultJson != null && resultJson.TrimStart().StartsWith("["))
            {
                var irList = JsonSerializer.Deserialize<List<QueryIr>>(resultJson, serializeOptions);
                return irList?.FirstOrDefault() ?? new QueryIr();
            }
            
            // Or if it returned multiple objects not in an array, try to just wrap it in an array
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

    public async Task<string> GetErrorSuggestionAsync(string errorMessage, string userQuery)
    {
        var systemPrompt = @"You are a helpful database assistant analyzing a SQL error.
The user provided a request, and it resulted in a database error.
Your job is to read the error and provide a short, friendly, and actionable suggestion in Romanian on how the user could fix their query or input data.
Do not output technical jargon if possible. Keep it under 2-3 sentences. Do not use markdown format.";

        var userPrompt = $"User input: {userQuery}\nDatabase Error: {errorMessage}\n\nPlease provide a short suggestion in Romanian.";

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



