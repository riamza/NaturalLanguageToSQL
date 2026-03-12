using System.Data;
using System.Data.Common;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Services;

public class QueryExecutionService : IQueryExecutionService
{
    private readonly ApplicationDbContext _dbContext;

    public QueryExecutionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<IDictionary<string, object>>> ExecuteQueryAsync(string sql, Dictionary<string, object> parameters)
    {
        var resultList = new List<IDictionary<string, object>>();

        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;

        if (wasClosed)
        {
            await connection.OpenAsync();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            foreach (var param in parameters)
            {
                var dbParam = command.CreateParameter();
                dbParam.ParameterName = param.Key;
                dbParam.Value = param.Value ?? DBNull.Value;
                command.Parameters.Add(dbParam);
            }

            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var rowInfo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    rowInfo[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                resultList.Add(rowInfo);
            }
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }

        return resultList;
    }
}