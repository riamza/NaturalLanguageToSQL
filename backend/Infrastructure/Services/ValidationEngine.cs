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
        
        if (string.IsNullOrWhiteSpace(ir.Table)) return (false, "Target table is empty.");
        
        if (ir.Action == "CREATE_TABLE")
        {
            if (ir.TableColumns == null || ir.TableColumns.Count == 0)
                return (false, "CREATE_TABLE requires at least one column definition.");
            return (true, string.Empty);
        }

        var model = _dbContext.Model;
        var entityType = model.GetEntityTypes().FirstOrDefault(t => t.GetTableName()?.Equals(ir.Table, StringComparison.OrdinalIgnoreCase) == true);

        if (entityType == null)
            return (false, "The target table is not configured within the secure domain contexts.");

        var validColumns = entityType.GetProperties().Select(p => p.GetColumnName() ?? p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ir.Action == "INSERT")
        {
            foreach (var col in ir.InsertColumns)
            {
                if (!validColumns.Contains(col))
                {
                    return (false, $"Disallowed or non-existent column '{col}' in INSERT clause.");
                }
            }
            if (ir.InsertValues == null || ir.InsertValues.Count == 0)
            {
                return (false, "No values provided for INSERT.");
            }
            return (true, string.Empty);
        }

        if (ir.Action == "UPDATE")
        {
            foreach (var clause in ir.SetClauses)
            {
                if (!validColumns.Contains(clause.Column))
                    return (false, $"Disallowed or non-existent column '{clause.Column}' in UPDATE SET clause.");
            }
        }

        foreach (var col in ir.SelectColumns)
        {
            if (col != "*" && !validColumns.Contains(col))
            {
                return (false, $"Disallowed or non-existent column '{col}' in SELECT clause.");
            }
        }

        foreach (var clause in ir.WhereClauses)
        {
            if (!validColumns.Contains(clause.Column))
                return (false, $"Disallowed or non-existent column '{clause.Column}' in WHERE clause.");

            if (!AllowedOperators.Contains(clause.Operator))
            {
                 return (false, $"Operator '{clause.Operator}' is not permitted for security reasons.");
            }
        }

        foreach (var order in ir.OrderClauses)
        {
            if (!validColumns.Contains(order.Column))
                return (false, $"Disallowed or non-existent column '{order.Column}' in ORDER BY clause.");
            
            var upperDir = order.Direction?.ToUpper();
            if (upperDir != "ASC" && upperDir != "DESC")
                return (false, "Order direction must be ASC or DESC.");
        }

        if (ir.Limit is <= 0 or > 1000)
        {
            return (false, "Limit must be strictly greater than 0 and less than or equal to 1000.");
        }

        return await Task.FromResult((true, string.Empty));
    }
}