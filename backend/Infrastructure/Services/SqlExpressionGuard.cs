using System.Text.RegularExpressions;

namespace Infrastructure.Services;

public static class SqlExpressionGuard
{
    private const int MaxExpressionLength = 400;

    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOW", "DATE_TRUNC", "DATE_PART", "EXTRACT", "AGE", "TO_CHAR", "TO_DATE", "TO_TIMESTAMP", "MAKE_DATE",
        "COUNT", "SUM", "AVG", "MIN", "MAX",
        "ROUND", "ABS", "CEIL", "CEILING", "FLOOR", "MOD", "POWER", "SQRT",
        "COALESCE", "NULLIF", "GREATEST", "LEAST",
        "LOWER", "UPPER", "LENGTH", "TRIM", "BTRIM", "CONCAT", "LEFT", "RIGHT", "SUBSTRING"
    };

    private static readonly HashSet<string> AllowedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "IS", "NULL", "TRUE", "FALSE", "IN", "BETWEEN", "LIKE", "ILIKE",
        "INTERVAL", "DISTINCT", "ASC", "DESC", "AS", "CASE", "WHEN", "THEN", "ELSE", "END",
        "CURRENT_DATE", "CURRENT_TIMESTAMP", "CURRENT_TIME", "DATE", "TIMESTAMP", "TIME", "FILTER"
    };

    private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "GRANT", "REVOKE", "EXEC", "EXECUTE", "MERGE", "COPY", "CALL", "DO",
        "UNION", "INTERSECT", "EXCEPT", "INTO", "RETURNING", "FROM", "WHERE", "JOIN",
        "USING", "SET", "VALUES", "SLEEP", "PG_SLEEP", "WAITFOR", "VACUUM", "ANALYZE", "LATERAL"
    };

    private static readonly string[] ForbiddenSubstrings =
    {
        ";", "--", "/*", "*/", "::", "pg_", "xp_", "information_schema", "\\", "0x", "$$", "@@", "`"
    };

    private static readonly Regex TokenRegex = new(
        @"\G\s*(?:" +
        @"(?<str>'(?:[^']|'')*')" +
        @"|(?<num>\d+(?:\.\d+)?)" +
        @"|(?<id>""?[A-Za-z_][A-Za-z0-9_]*""?(?:\.(?:\*|""?[A-Za-z_][A-Za-z0-9_]*""?))*)" +
        @"|(?<op><=|>=|<>|!=|[-+*/%=<>(),])" +
        @")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SafeAlias = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static (bool IsValid, string Error) Validate(string? expression, ISet<string> validColumns)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return (false, "Expresia este goală.");

        var expr = expression.Trim();

        if (expr.Length > MaxExpressionLength)
            return (false, "Expresia depășește lungimea maximă permisă.");

        foreach (var bad in ForbiddenSubstrings)
        {
            if (expr.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                return (false, $"Secvență interzisă în expresie: '{bad}'.");
        }

        var tokens = new List<(string Kind, string Text)>();
        int pos = 0;
        int parenDepth = 0;

        while (pos < expr.Length)
        {
            var m = TokenRegex.Match(expr, pos);
            if (!m.Success || m.Length == 0)
                return (false, $"Caracter nepermis în expresie la poziția {pos}.");

            if (m.Groups["str"].Success) tokens.Add(("str", m.Groups["str"].Value));
            else if (m.Groups["num"].Success) tokens.Add(("num", m.Groups["num"].Value));
            else if (m.Groups["id"].Success) tokens.Add(("id", m.Groups["id"].Value));
            else if (m.Groups["op"].Success) tokens.Add(("op", m.Groups["op"].Value));

            pos += m.Length;
        }

        if (tokens.Count == 0)
            return (false, "Expresia nu conține tokeni valizi.");

        for (int i = 0; i < tokens.Count; i++)
        {
            var (kind, text) = tokens[i];

            if (kind == "op")
            {
                if (text == "(") parenDepth++;
                else if (text == ")") { parenDepth--; if (parenDepth < 0) return (false, "Paranteze dezechilibrate în expresie."); }
                continue;
            }

            if (kind == "str" || kind == "num") continue;

            var bareName = text.Trim('"');

            if (bareName.Contains('.')) continue;

            var upper = bareName.ToUpperInvariant();

            if (ForbiddenKeywords.Contains(upper))
                return (false, $"Cuvânt-cheie interzis în expresie: '{bareName}'.");

            bool followedByParen = i + 1 < tokens.Count && tokens[i + 1].Kind == "op" && tokens[i + 1].Text == "(";

            if (followedByParen)
            {
                if (!AllowedFunctions.Contains(upper))
                    return (false, $"Funcție nepermisă în expresie: '{bareName}'.");
                continue;
            }

            if (AllowedKeywords.Contains(upper)) continue;
            if (validColumns.Contains(bareName)) continue;

            return (false, $"Identificator necunoscut în expresie: '{bareName}'. Sunt permise doar coloane cunoscute, funcții și constante din lista albă.");
        }

        if (parenDepth != 0)
            return (false, "Paranteze dezechilibrate în expresie.");

        return (true, string.Empty);
    }

    public static (bool IsValid, string Error) ValidateJoinCondition(string? condition, ISet<string> validColumns)
        => Validate(condition, validColumns);

    public static (bool IsValid, string Error) ValidateSelectColumn(string column, ISet<string> validColumns)
    {
        if (string.IsNullOrWhiteSpace(column)) return (false, "Coloană goală în SELECT.");
        var col = column.Trim();
        if (col == "*") return (true, string.Empty);

        string expr = col;
        var asIdx = col.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
        if (asIdx >= 0)
        {
            expr = col.Substring(0, asIdx).Trim();
            var alias = col.Substring(asIdx + 4).Trim().Trim('"');
            if (!SafeAlias.IsMatch(alias))
                return (false, $"Alias invalid în SELECT: '{alias}'.");
        }

        if (!expr.Contains('(') && !expr.Contains('.') && !expr.Contains(' '))
        {
            return validColumns.Contains(expr.Trim('"'))
                ? (true, string.Empty)
                : (false, $"Coloană inexistentă în SELECT: '{expr}'.");
        }

        if (!expr.Contains('(') && Regex.IsMatch(expr, @"^""?[A-Za-z_][A-Za-z0-9_]*""?\.(?:\*|""?[A-Za-z_][A-Za-z0-9_]*""?)$"))
            return (true, string.Empty);

        return Validate(expr, validColumns);
    }
}
