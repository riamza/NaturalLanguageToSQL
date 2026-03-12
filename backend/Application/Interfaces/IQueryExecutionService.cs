namespace Application.Interfaces;

public interface IQueryExecutionService
{
    /// <summary>
    /// Executes a dynamic SQL Select query and maps the result sets into dynamic objects/dictionaries.
    /// This strictly runs as a read-only context in the DB level.
    /// </summary>
    /// <param name="sql">The prepared parameterised SQL query</param>
    /// <param name="parameters">Parameters required by the query</param>
    /// <returns>A list of dictionaries representing dynamically returned rows</returns>
    Task<IEnumerable<IDictionary<string, object>>> ExecuteQueryAsync(string sql, Dictionary<string, object> parameters);
}