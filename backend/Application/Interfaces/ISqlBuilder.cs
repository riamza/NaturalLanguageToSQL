using Core.Models;

namespace Application.Interfaces;

public interface ISqlBuilder
{
    /// <summary>
    /// Safely builds PostgreSQL syntax given an Intermediate Representation (IR).
    /// Generates parameterized arguments to prevent immediate injection on the query itself.
    /// </summary>
    /// <param name="ir">The intermediate representation to build SQL from</param>
    /// <returns>A Sql query string along with parameterized values</returns>
    (string Sql, Dictionary<string, object> Parameters) BuildSql(QueryIr ir);
}