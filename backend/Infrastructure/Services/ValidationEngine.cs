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

        var validColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool tableExists = false;

        using (var command = _dbContext.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @tableName;";
            var param = command.CreateParameter();
            param.ParameterName = "@tableName";
            param.Value = ir.Table.ToLower();
            command.Parameters.Add(param);

            if (command.Connection.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tableExists = true;
                    validColumns.Add(reader.GetString(0));
                }
            }
        }

        if (!tableExists)
            return (false, "The target table is not configured within the secure domain contexts.");

        if (ir.Action == "INSERT" || ir.Action == "UPSERT")
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
            if (col == "*") continue;
            
            // If it has aggregates or prefixes (JOINS), allow it
            if (col.Contains("(") || col.Contains(" AS ") || col.Contains(".")) continue;

            if (!validColumns.Contains(col))
            {
                return (false, $"Disallowed or non-existent column '{col}' in SELECT clause.");
            }
        }

        foreach (var clause in ir.WhereClauses)
        {
            if (clause.Column.Contains(".")) continue; // Allow foreign table where clause filtering if joined.

            if (!validColumns.Contains(clause.Column))

                return (false, $"Disallowed or non-existent column '{clause.Column}' in WHERE clause.");

            if (!AllowedOperators.Contains(clause.Operator))
            {
                 return (false, $"Operator '{clause.Operator}' is not permitted for security reasons.");
            }
        }

        foreach (var order in ir.OrderClauses)
        {
            if (!validColumns.Contains(order.Column) && !ir.SelectColumns.Any(sc => sc.Contains($" AS \"{order.Column}\"") || sc.Contains($" AS {order.Column}")) && !order.Column.Contains("."))
            {
                return (false, $"Disallowed or non-existent column '{order.Column}' in ORDER BY clause.");
            }

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
