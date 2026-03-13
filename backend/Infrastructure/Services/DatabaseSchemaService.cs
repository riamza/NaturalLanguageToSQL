using System.Text;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class DatabaseSchemaService : IDatabaseSchemaService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseSchemaService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string SchemaQuery = @"
        WITH fk_info AS (
            SELECT
                kcu.table_name,
                kcu.column_name,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM
                information_schema.table_constraints AS tc
                JOIN information_schema.key_column_usage AS kcu
                  ON tc.constraint_name = kcu.constraint_name
                  AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage AS ccu
                  ON ccu.constraint_name = tc.constraint_name
                  AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
        )
        SELECT
            t.table_name,
            c.column_name,
            c.data_type,
            CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN true ELSE false END as is_primary,
            fk.foreign_table_name,
            fk.foreign_column_name
        FROM
            information_schema.tables t
        JOIN
            information_schema.columns c ON t.table_name = c.table_name
        LEFT JOIN information_schema.key_column_usage kcu 
            ON t.table_name = kcu.table_name AND c.column_name = kcu.column_name
        LEFT JOIN information_schema.table_constraints tc 
            ON kcu.constraint_name = tc.constraint_name AND tc.constraint_type = 'PRIMARY KEY'
        LEFT JOIN fk_info fk
            ON t.table_name = fk.table_name AND c.column_name = fk.column_name
        WHERE
            t.table_schema = 'public'
            AND t.table_type = 'BASE TABLE'
            AND t.table_name != '__EFMigrationsHistory'
        ORDER BY
            t.table_name, c.ordinal_position;";

    public async Task<string> GetDatabaseSchemaDescriptionAsync()
    {
        var schemaBuilder = new StringBuilder();

        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = SchemaQuery;

        await _dbContext.Database.OpenConnectionAsync();

        using var result = await command.ExecuteReaderAsync();

        var currentTable = string.Empty;
        var hasColumns = false;

        while (await result.ReadAsync())
        {
            var tableName = result.GetString(0);
            var columnName = result.GetString(1);
            // Index 2 is data_type, Index 3 is is_primary
            var foreignTable = result.IsDBNull(4) ? null : result.GetString(4);
            var foreignCol = result.IsDBNull(5) ? null : result.GetString(5);

            if (currentTable != tableName)
            {
                if (hasColumns) schemaBuilder.AppendLine("] ");
                schemaBuilder.Append($"Table '{tableName}' with columns: [");
                currentTable = tableName;
                hasColumns = false;
            }

            if (hasColumns) schemaBuilder.Append(", ");
            
            if (foreignTable != null) {
                schemaBuilder.Append($"'{columnName}' (FK to {foreignTable}.{foreignCol})");
            } else {
                schemaBuilder.Append($"'{columnName}'");
            }
            
            hasColumns = true;
        }

        if (hasColumns) schemaBuilder.AppendLine("]");

        await _dbContext.Database.CloseConnectionAsync();

        return schemaBuilder.ToString();
    }

    public async Task<List<object>> GetDatabaseSchemaJsonAsync()
    {
        var tables = new List<object>();

        using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = SchemaQuery;

        await _dbContext.Database.OpenConnectionAsync();
        using var result = await command.ExecuteReaderAsync();

        string currentTable = null;
        var currentColumns = new List<object>();

        while (await result.ReadAsync())
        {
            var tableName = result.GetString(0);
            var columnName = result.GetString(1);
            var dataType = result.GetString(2);
            var isPrimary = !result.IsDBNull(3) && result.GetBoolean(3);
            var foreignTable = result.IsDBNull(4) ? null : result.GetString(4);
            var foreignColumn = result.IsDBNull(5) ? null : result.GetString(5);

            if (currentTable != tableName)
            {
                if (currentTable != null)
                {
                    tables.Add(new
                    {
                        name = currentTable,
                        description = "Database table",
                        columns = currentColumns.ToList()
                    });
                }
                currentTable = tableName;
                currentColumns.Clear();
            }

            currentColumns.Add(new
            {
                name = columnName,
                type = dataType,
                isPrimary = isPrimary,
                foreignKeyContext = foreignTable != null ? $"{foreignTable}({foreignColumn})" : null
            });
        }

        if (currentTable != null)
        {
            tables.Add(new
            {
                name = currentTable,
                description = "Database table",
                columns = currentColumns.ToList()
            });
        }

        await _dbContext.Database.CloseConnectionAsync();
        return tables;
    }
}