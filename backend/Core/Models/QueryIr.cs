namespace Core.Models;

public class QueryIr
{
    public string Action { get; set; } = "SELECT"; // "SELECT" or "INSERT"
    public string Table { get; set; } = string.Empty;
    public List<string> SelectColumns { get; set; } = new();
    public List<WhereClause> WhereClauses { get; set; } = new();
    public List<OrderClause> OrderClauses { get; set; } = new();
    public int? Limit { get; set; }

    public List<string> InsertColumns { get; set; } = new();
    public List<List<object>> InsertValues { get; set; } = new();
    
    // For UPSERT / CREATE OR UPDATE
    public List<string> ConflictColumns { get; set; } = new();
    public List<string> UpdateColumns { get; set; } = new();

    // For CREATE_TABLE
    public List<ColumnDefinition> TableColumns { get; set; } = new();

    // For UPDATE
    public List<SetClause> SetClauses { get; set; } = new();

    // Used when the query is non-sensical or impossible
    public string ErrorDetails { get; set; } = string.Empty;
}

public class SetClause
{
    public string Column { get; set; } = string.Empty;
    public object Value { get; set; } = string.Empty; // Can be string, number, etc.
}

public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // e.g., "INTEGER", "VARCHAR", "BOOLEAN", "TIMESTAMP"
    public bool IsPrimaryKey { get; set; }
    public bool IsNullable { get; set; } = true;
    public string? ReferencesTable { get; set; }
    public string? ReferencesColumn { get; set; }
}

public class WhereClause
{
    public string Column { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty; // e.g., "=", ">", "<", "LIKE", "ILIKE"
    public object Value { get; set; } = string.Empty; // Can be string, number, etc.
}

public class OrderClause
{
    public string Column { get; set; } = string.Empty;
    public string Direction { get; set; } = "ASC"; // "ASC" or "DESC"
}