using System.Text;
using Application.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

public class PostgresSqlBuilder : ISqlBuilder
{
    public (string Sql, Dictionary<string, object> Parameters) BuildSql(QueryIr ir)
    {
        return ir.Action switch
        {
            "CREATE_TABLE" => BuildCreateTableSql(ir),
            "INSERT" or "UPSERT" => BuildInsertOrUpsertSql(ir),
            "UPDATE" => BuildUpdateSql(ir),
            "DELETE" => BuildDeleteSql(ir),
            _ => BuildSelectSql(ir)
        };
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildCreateTableSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        sqlBuilder.Append($"CREATE TABLE \"{ir.Table}\" (\n");
        
        var columnDefs = new List<string>();
        foreach (var col in ir.TableColumns)
        {
            var def = $"    \"{col.Name}\" {col.DataType}";
            if (!col.IsNullable) def += " NOT NULL";
            if (col.IsPrimaryKey) def += " PRIMARY KEY";
            if (!string.IsNullOrEmpty(col.ReferencesTable) && !string.IsNullOrEmpty(col.ReferencesColumn))
            {
                def += $" REFERENCES \"{col.ReferencesTable}\" (\"{col.ReferencesColumn}\")";
            }
            columnDefs.Add(def);
        }
        
        sqlBuilder.Append(string.Join(",\n", columnDefs));
        sqlBuilder.Append("\n)");
        return (sqlBuilder.ToString(), parameters);
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildInsertOrUpsertSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        AppendInsertColumns(ir, sqlBuilder);
        AppendInsertValues(ir, sqlBuilder, parameters);
        AppendConflictClause(ir, sqlBuilder);

        sqlBuilder.Append(" RETURNING *");
        return (sqlBuilder.ToString(), parameters);
    }

    private void AppendInsertColumns(QueryIr ir, StringBuilder sqlBuilder)
    {
        var columns = string.Join(", ", ir.InsertColumns.Select(c => $"\"{c}\""));
        sqlBuilder.Append($"INSERT INTO \"{ir.Table}\" ({columns}) VALUES ");
    }

    private void AppendInsertValues(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters)
    {
        for (int i = 0; i < ir.InsertValues.Count; i++)
        {
            var row = ir.InsertValues[i];
            var rowParams = new List<string>();
            for (int j = 0; j < row.Count; j++)
            {
                var paramName = $"@p_{i}_{j}";
                rowParams.Add(paramName);
                parameters.Add(paramName, GetValue(row[j]));
            }
            sqlBuilder.Append($"({string.Join(", ", rowParams)})");
            if (i < ir.InsertValues.Count - 1)
            {
                sqlBuilder.Append(", ");
            }
        }
    }

    private void AppendConflictClause(QueryIr ir, StringBuilder sqlBuilder)
    {
        var conflictColsList = (ir.ConflictColumns != null && ir.ConflictColumns.Count > 0) 
            ? ir.ConflictColumns 
            : new List<string> { "Id" };

        var conflictColsStr = string.Join(", ", conflictColsList.Select(c => $"\"{c}\""));
        sqlBuilder.Append($" ON CONFLICT ({conflictColsStr}) DO UPDATE SET ");

        var updateColsList = (ir.UpdateColumns != null && ir.UpdateColumns.Count > 0) 
            ? ir.UpdateColumns 
            : ir.InsertColumns.Where(c => !conflictColsList.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

        if (updateColsList.Count > 0)
        {
            var setClausesList = updateColsList.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
            sqlBuilder.Append(string.Join(", ", setClausesList));
        }
        else
        {
            // Fallback if no specific update columns
            var fallbackClauses = conflictColsList.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
            sqlBuilder.Append(string.Join(", ", fallbackClauses));
        }
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildUpdateSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        sqlBuilder.Append($"UPDATE \"{ir.Table}\" SET ");

        var setList = new List<string>();
        for (int i = 0; i < ir.SetClauses.Count; i++)
        {
            var clause = ir.SetClauses[i];
            if (clause.IsExpression)
            {
                var valStr = GetValue(clause.Value)?.ToString() ?? "NULL";
                setList.Add($"\"{clause.Column}\" = {valStr}");
            }
            else
            {
                var paramName = $"@s{i}";
                setList.Add($"\"{clause.Column}\" = {paramName}");
                parameters.Add(paramName, GetValue(clause.Value));
            }
        }
        sqlBuilder.Append(string.Join(", ", setList));

        BuildWhereClause(ir, sqlBuilder, parameters, ir.SetClauses.Count);

        sqlBuilder.Append(" RETURNING *");
        return (sqlBuilder.ToString(), parameters);
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildDeleteSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        int paramCounter = 0;
        BuildCascadeDeletes(ir, sqlBuilder, parameters, ref paramCounter);

        sqlBuilder.Append($"DELETE FROM \"{ir.Table}\"");
        BuildWhereClause(ir, sqlBuilder, parameters, paramCounter);
        sqlBuilder.Append(" RETURNING *");
        return (sqlBuilder.ToString(), parameters);
    }

    private void BuildCascadeDeletes(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (ir.CascadeDeletes != null && ir.CascadeDeletes.Count > 0)
        {
            foreach (var cascade in ir.CascadeDeletes)
            {
                BuildCascadeDeletes(cascade, sqlBuilder, parameters, ref paramCounter);

                sqlBuilder.Append($"DELETE FROM \"{cascade.Table}\"");
                paramCounter = BuildWhereClauseInternal(cascade, sqlBuilder, parameters, paramCounter);
                sqlBuilder.Append(";\n");
            }
        }
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildSelectSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        return BuildSelectSqlInternal(ir, parameters, ref paramCounter);
    }

    private int paramCounter = 0;

    private (string Sql, Dictionary<string, object> Parameters) BuildSelectSqlInternal(QueryIr ir, Dictionary<string, object> parameters, ref int counter)
    {
        var sqlBuilder = new StringBuilder();

        AppendSelectFields(ir, sqlBuilder);
        AppendJoins(ir, sqlBuilder);
        counter = BuildWhereClauseInternal(ir, sqlBuilder, parameters, counter);
        AppendGroupBy(ir, sqlBuilder);
        AppendOrderClauses(ir, sqlBuilder);
        AppendLimit(ir, sqlBuilder);
        AppendUnions(ir, sqlBuilder, parameters, ref counter);

        return (sqlBuilder.ToString(), parameters);
    }

    private void AppendSelectFields(QueryIr ir, StringBuilder sqlBuilder)
    {
        var selectFields = ir.SelectColumns == null || ir.SelectColumns.Count == 0
            ? "*"
            : string.Join(", ", ir.SelectColumns.Select(QuoteIdentifier));

        sqlBuilder.Append($"SELECT {selectFields} FROM \"{ir.Table}\"");
    }

    private void AppendJoins(QueryIr ir, StringBuilder sqlBuilder)
    {
        if (ir.Joins != null && ir.Joins.Count > 0)
        {
            foreach (var join in ir.Joins)
            {
                sqlBuilder.Append($" {join.Type.ToUpper()} JOIN \"{join.Table}\" ON {join.Condition}");
            }
        }
    }

    private void AppendGroupBy(QueryIr ir, StringBuilder sqlBuilder)
    {
        if (ir.GroupBy != null && ir.GroupBy.Count > 0)
        {
            sqlBuilder.Append(" GROUP BY ");
            sqlBuilder.Append(string.Join(", ", ir.GroupBy.Select(QuoteIdentifier)));
        }
    }

    private void AppendOrderClauses(QueryIr ir, StringBuilder sqlBuilder)
    {
        if (ir.OrderClauses != null && ir.OrderClauses.Count > 0)
        {
            sqlBuilder.Append(" ORDER BY ");
            var orderings = ir.OrderClauses.Select(o => $"{QuoteIdentifier(o.Column)} {o.Direction}");
            sqlBuilder.Append(string.Join(", ", orderings));
        }
    }

    private void AppendLimit(QueryIr ir, StringBuilder sqlBuilder)
    {
        if (ir.Limit.HasValue)
        {
            sqlBuilder.Append($" LIMIT {ir.Limit.Value}");
        }
    }

    private void AppendUnions(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters, ref int counter)
    {
        if (ir.Unions != null && ir.Unions.Count > 0)
        {
            foreach (var unionIr in ir.Unions)
            {
                var unionResult = BuildSelectSqlInternal(unionIr, parameters, ref counter);
                sqlBuilder.Append($" UNION {unionResult.Sql}");
            }
        }
    }

    private void BuildWhereClause(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters, int paramOffset)
    {
        BuildWhereClauseInternal(ir, sqlBuilder, parameters, paramOffset);
    }

    private int BuildWhereClauseInternal(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters, int paramOffset)
    {
        int offset = paramOffset;
        if (ir.WhereClauses != null && ir.WhereClauses.Count > 0)
        {
            sqlBuilder.Append(" WHERE ");
            for (int i = 0; i < ir.WhereClauses.Count; i++)
            {
                var clause = ir.WhereClauses[i];
                
                if (i > 0) sqlBuilder.Append(" AND ");

                var colName = QuoteIdentifier(clause.Column);

                if (clause.IsExpression)
                {
                    var valStr = clause.Value?.ToString() ?? "NULL";
                    sqlBuilder.Append($"{colName} {clause.Operator} {valStr}");
                }
                else
                {
                    var paramName = $"@p{offset++}";
                    sqlBuilder.Append($"{colName} {clause.Operator} {paramName}");
                    parameters.Add(paramName, GetValue(clause.Value));
                }
            }
        }
        return offset;
    }

    private string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier == "*") return identifier;

        if (identifier.Contains(" AS ", StringComparison.OrdinalIgnoreCase))
        {
            var idx = identifier.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            var expr = identifier.Substring(0, idx);
            var alias = identifier.Substring(idx + 4).Trim().Trim('"');
            return $"{expr} AS \"{alias}\"";
        }

        if (identifier.Contains("(")) return identifier;

        if (identifier.Contains("."))
        {
            var parts = identifier.Split('.');
            var quotedParts = parts.Select(p => p == "*" ? p : $"\"{p.Trim('"')}\"");
            return string.Join(".", quotedParts);
        }
        
        return $"\"{identifier.Trim('"')}\"";
    }

    private object GetValue(object rawValue)
    {
        if (rawValue is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => 
                    DateTime.TryParse(jsonElement.GetString(), out var dateVal) && jsonElement.GetString()!.Contains("-") && jsonElement.GetString()!.Length >= 10 
                        ? dateVal.ToUniversalTime() 
                        : (object)jsonElement.GetString()!,
                System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt32(out var intVal) ? intVal : jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => jsonElement.ToString()
            };
        }
        return rawValue;
    }
}