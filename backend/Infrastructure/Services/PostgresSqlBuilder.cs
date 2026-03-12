using System.Text;
using Application.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

public class PostgresSqlBuilder : ISqlBuilder
{
    public (string Sql, Dictionary<string, object> Parameters) BuildSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        // SELECT Clause
        var selectFields = ir.SelectColumns == null || ir.SelectColumns.Count == 0 
            ? "*" 
            : string.Join(", ", ir.SelectColumns.Select(c => c == "*" ? "*" : $"\"{c}\""));
        
        sqlBuilder.Append($"SELECT {selectFields} FROM \"{ir.Table}\"");

        // WHERE Clause
        if (ir.WhereClauses != null && ir.WhereClauses.Count > 0)
        {
            sqlBuilder.Append(" WHERE ");
            for (int i = 0; i < ir.WhereClauses.Count; i++)
            {
                var clause = ir.WhereClauses[i];
                var paramName = $"@p{i}";
                
                if (i > 0) sqlBuilder.Append(" AND ");
                
                sqlBuilder.Append($"\"{clause.Column}\" {clause.Operator} {paramName}");
                
                // Convert JSON elements to actual types if needed, for now assign directly
                parameters.Add(paramName, GetValue(clause.Value));
            }
        }

        // ORDER BY Clause
        if (ir.OrderClauses != null && ir.OrderClauses.Count > 0)
        {
            sqlBuilder.Append(" ORDER BY ");
            var orderings = ir.OrderClauses.Select(o => $"\"{o.Column}\" {o.Direction}");
            sqlBuilder.Append(string.Join(", ", orderings));
        }

        // LIMIT Clause
        if (ir.Limit.HasValue)
        {
            sqlBuilder.Append($" LIMIT {ir.Limit.Value}");
        }

        return (sqlBuilder.ToString(), parameters);
    }

    private object GetValue(object rawValue)
    {
        if (rawValue is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString()!,
                System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt32(out var intVal) ? intVal : jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => jsonElement.ToString()
            };
        }
        return rawValue;
    }
}