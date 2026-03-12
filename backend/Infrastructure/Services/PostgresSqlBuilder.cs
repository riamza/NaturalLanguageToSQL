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

        var columns = string.Join(", ", ir.InsertColumns.Select(c => $"\"{c}\""));
        sqlBuilder.Append($"INSERT INTO \"{ir.Table}\" ({columns}) VALUES ");

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
            var fallbackClauses = conflictColsList.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
            sqlBuilder.Append(string.Join(", ", fallbackClauses));
        }

        sqlBuilder.Append(" RETURNING *");
        return (sqlBuilder.ToString(), parameters);
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
            var paramName = $"@s{i}";
            setList.Add($"\"{clause.Column}\" = {paramName}");
            parameters.Add(paramName, GetValue(clause.Value));
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

        sqlBuilder.Append($"DELETE FROM \"{ir.Table}\"");
        BuildWhereClause(ir, sqlBuilder, parameters, 0);
        sqlBuilder.Append(" RETURNING *");
        return (sqlBuilder.ToString(), parameters);
    }

    private (string Sql, Dictionary<string, object> Parameters) BuildSelectSql(QueryIr ir)
    {
        var parameters = new Dictionary<string, object>();
        var sqlBuilder = new StringBuilder();

        var selectFields = ir.SelectColumns == null || ir.SelectColumns.Count == 0
            ? "*"
            : string.Join(", ", ir.SelectColumns.Select(c => c == "*" ? "*" : $"\"{c}\""));

        sqlBuilder.Append($"SELECT {selectFields} FROM \"{ir.Table}\"");

        BuildWhereClause(ir, sqlBuilder, parameters, 0);

        if (ir.OrderClauses != null && ir.OrderClauses.Count > 0)
        {
            sqlBuilder.Append(" ORDER BY ");
            var orderings = ir.OrderClauses.Select(o => $"\"{o.Column}\" {o.Direction}");
            sqlBuilder.Append(string.Join(", ", orderings));
        }

        if (ir.Limit.HasValue)
        {
            sqlBuilder.Append($" LIMIT {ir.Limit.Value}");
        }

        return (sqlBuilder.ToString(), parameters);
    }

    private void BuildWhereClause(QueryIr ir, StringBuilder sqlBuilder, Dictionary<string, object> parameters, int paramOffset)
    {
        if (ir.WhereClauses != null && ir.WhereClauses.Count > 0)
        {
            sqlBuilder.Append(" WHERE ");
            for (int i = 0; i < ir.WhereClauses.Count; i++)
            {
                var clause = ir.WhereClauses[i];
                var paramName = $"@p{i + paramOffset}";

                if (i > 0) sqlBuilder.Append(" AND ");

                sqlBuilder.Append($"\"{clause.Column}\" {clause.Operator} {paramName}");

                parameters.Add(paramName, GetValue(clause.Value));
            }
        }
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