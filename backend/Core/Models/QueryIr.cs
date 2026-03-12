namespace Core.Models;

public class QueryIr
{
    public string Table { get; set; } = string.Empty;
    public List<string> SelectColumns { get; set; } = new();
    public List<WhereClause> WhereClauses { get; set; } = new();
    public List<OrderClause> OrderClauses { get; set; } = new();
    public int? Limit { get; set; }
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