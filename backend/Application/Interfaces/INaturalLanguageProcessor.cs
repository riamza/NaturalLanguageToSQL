using Core.Models;

namespace Application.Interfaces;

public interface INaturalLanguageProcessor
{
    /// <summary>
    /// Translates a natural language user query into an Intermediate Representation (IR).
    /// </summary>
    /// <param name="naturalLanguageQuery">The user's input, e.g., "Find all IT employees earning more than 50000"</param>
    /// <returns>A mapped Intermediate Representation (IR)</returns>
    Task<QueryIr> TranslateToIrAsync(string naturalLanguageQuery);
}