using Application.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class ValidationEngine : IValidationEngine
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ValidationEngine> _logger;

    // A white list of strongly safe operators we allow from the LLM
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", ">", "<", ">=", "<=", "<>", "!=", "LIKE", "ILIKE", "IN", "NOT IN", "IS NULL", "IS NOT NULL"
    };

    public ValidationEngine(ApplicationDbContext dbContext, ILogger<ValidationEngine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateIrAsync(QueryIr ir)
    {
        if (ir == null) return (false, "The IR is null.");
        
        // 1. Check Table
        if (string.IsNullOrWhiteSpace(ir.Table)) return (false, "Target table is empty.");
        
        // In this constrained assistant, maybe we only allow the "employees" table
        if (!ir.Table.Equals("employees", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Querying table '{ir.Table}' is not allowed.");
        }

        // Fetch Metadata representation for the explicit Employee entity constraints
        var model = _dbContext.Model;
        var entityType = model.GetEntityTypes().FirstOrDefault(t => t.GetTableName()?.Equals("employees", StringComparison.OrdinalIgnoreCase) == true);
        
        if (entityType == null)
            return (false, "The target table is not configured within the secure domain contexts.");

        var validColumns = entityType.GetProperties().Select(p => p.GetColumnName() ?? p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Check Select Columns
        foreach (var col in ir.SelectColumns)
        {
            if (col != "*" && !validColumns.Contains(col))
            {
                return (false, $"Disallowed or non-existent column '{col}' in SELECT clause.");
            }
        }

        // 3. Check Where Clauses & Filter operations
        foreach (var clause in ir.WhereClauses)
        {
            if (!validColumns.Contains(clause.Column))
                return (false, $"Disallowed or non-existent column '{clause.Column}' in WHERE clause.");

            if (!AllowedOperators.Contains(clause.Operator))
            {
                 return (false, $"Operator '{clause.Operator}' is not permitted for security reasons.");
            }
            
            // Note: Preventing direct raw drops/injects within Value strings will be managed in ISqlBuilder via parameterized queries
        }

        // 4. Check Ordering
        foreach (var order in ir.OrderClauses)
        {
            if (!validColumns.Contains(order.Column))
                return (false, $"Disallowed or non-existent column '{order.Column}' in ORDER BY clause.");
            
            var upperDir = order.Direction?.ToUpper();
            if (upperDir != "ASC" && upperDir != "DESC")
                return (false, "Order direction must be ASC or DESC.");
        }

        // 5. Basic limit check to prevent massive dumps
        if (ir.Limit is <= 0 or > 1000)
        {
            return (false, "Limit must be strictly greater than 0 and less than or equal to 1000.");
        }

        return await Task.FromResult((true, string.Empty));
    }
}