using Core.Models;

namespace Application.Interfaces;

public interface IValidationEngine
{
    /// <summary>
    /// Validates the Intermediate Representation against the database schema to ensure safety and correctness.
    /// Rules: Table exists, columns exist, operations are purely analytical (no inserts, drops, etc.).
    /// </summary>
    /// <param name="ir">The intermediate representation to validate</param>
    /// <returns>A tuple indicating if the IR is valid, and an error message if invalid.</returns>
    Task<(bool IsValid, string ErrorMessage)> ValidateIrAsync(QueryIr ir);
}