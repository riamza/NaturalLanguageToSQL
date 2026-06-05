using System.Text.RegularExpressions;
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

    private static readonly Regex SafeIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static bool IsSafeIdentifier(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier) && SafeIdentifierRegex.IsMatch(identifier.Trim().Trim('"'));

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

        foreach (var join in ir.Joins)
        {
            var upperType = join.Type?.ToUpperInvariant();
            if (upperType != "INNER" && upperType != "LEFT" && upperType != "RIGHT" && upperType != "FULL")
                return (false, $"Tip de JOIN nepermis: '{join.Type}'.");

            if (!IsSafeIdentifier(join.Table))
                return (false, $"Nume de tabelă invalid în JOIN: '{join.Table}'.");

            var joinCheck = SqlExpressionGuard.ValidateJoinCondition(join.Condition, validColumns);
            if (!joinCheck.IsValid)
                return (false, $"Condiție de JOIN nesigură: {joinCheck.Error}");
        }

        if (ir.Action == "UPDATE")
        {
            foreach (var clause in ir.SetClauses)
            {
                if (!validColumns.Contains(clause.Column))
                    return (false, $"Disallowed or non-existent column '{clause.Column}' in UPDATE SET clause.");

                if (clause.IsExpression)
                {
                    var exprCheck = SqlExpressionGuard.Validate(clause.Value?.ToString(), validColumns);
                    if (!exprCheck.IsValid)
                        return (false, $"Expresie nesigură în clauza SET pentru '{clause.Column}': {exprCheck.Error}");
                }
            }
        }

        foreach (var col in ir.SelectColumns)
        {
            var selCheck = SqlExpressionGuard.ValidateSelectColumn(col, validColumns);
            if (!selCheck.IsValid)
                return (false, selCheck.Error);
        }

        foreach (var col in ir.GroupBy)
        {
            var grpCheck = SqlExpressionGuard.ValidateSelectColumn(col, validColumns);
            if (!grpCheck.IsValid)
                return (false, $"Expresie nesigură în GROUP BY: {grpCheck.Error}");
        }

        foreach (var clause in ir.WhereClauses)
        {
            if (!AllowedOperators.Contains(clause.Operator))
            {
                return (false, $"Operator '{clause.Operator}' is not permitted for security reasons.");
            }

            if (clause.IsExpression)
            {
                var exprCheck = SqlExpressionGuard.Validate(clause.Value?.ToString(), validColumns);
                if (!exprCheck.IsValid)
                    return (false, $"Expresie nesigură în clauza WHERE pentru '{clause.Column}': {exprCheck.Error}");
            }

            if (clause.Column.Contains(".")) continue; // Allow foreign table where clause filtering if joined.

            if (!validColumns.Contains(clause.Column))
                return (false, $"Disallowed or non-existent column '{clause.Column}' in WHERE clause.");
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
