namespace RedisQ.Core.Runtime;

internal class ExprPrinter
{
    private static readonly string Br = Environment.NewLine;

    private int _indent;

    private string Indent => new(' ', 4 * _indent);

    public string Print(Expr expr) =>
        expr switch
        {
            ProgramExpr e => string.Join(";" + Br, e.Children.Select(Print)) + ';',
            LiteralExpr e => PrintLiteral(e.Literal),
            TupleExpr e => PrintTuple(e),
            ListExpr e => PrintList(e),
            IdentExpr e => e.Ident,
            FunctionInvocationExpr e => "",
            OrExpr e => PrintBinary(e, "||"),
            AndExpr e => PrintBinary(e, "&&"),
            EqExpr e => PrintBinary(e, "=="),
            NeExpr e => PrintBinary(e, "!="),
            MatchExpr e => PrintBinary(e, "~="),
            NullCoalescingExpr e => PrintBinary(e, "??"),
            LtExpr e => PrintBinary(e, "<"),
            LeExpr e => PrintBinary(e, "<="),
            GtExpr e => PrintBinary(e, ">"),
            GeExpr e => PrintBinary(e, ">="),
            RangeExpr e => PrintBinary(e, ".."),
            PlusExpr e => PrintBinary(e, "+"),
            MinusExpr e => PrintBinary(e, "-"),
            TimesExpr e => PrintBinary(e, "*"),
            DivExpr e => PrintBinary(e, "/"),
            ModExpr e => PrintBinary(e, "%"),
            NegExpr e => "-" + Print(e.Operand),
            PosExpr e => "+" + Print(e.Operand),
            NotExpr e => "!" + Print(e.Operand),
            SubscriptExpr e => $"{Print(e.Operand)}[{Print(e.Subscript)}]",
            FieldAccessExpr e => Print(e.Operand) + '.' + e.FieldName,
            TernaryExpr e => Print(e.Condition) + " ? " + Print(e.TrueCase) + " : " + Print(e.FalseCase),
            FromExpr e => PrintFrom(e),
            EagerFromExpr e => PrintFrom(e.From),
            FromClause e => $"from {e.Ident} in {Print(e.Source)}",
            WhereClause e => $"where {Print(e.Predicate)}",
            LimitClause e => e.Offset is null
                ? $"limit {Print(e.Count)}"
                : $"limit {Print(e.Count)} offset {Print(e.Offset)}",
            OrderByClause e => e.IsDescending
                ? $"orderby {Print(e.Key)} descending"
                : $"orderby {Print(e.Key)}",
            GroupByClause e => $"group {Print(e.Value)} by {Print(e.Key)} into {e.Ident}",
            LetClause e => $"let {e.Ident} = {Print(e.Right)}",
            ThrowExpr e => "throw " + Print(e.Exception),
            FuncBinding e => PrintFunction(e),
            LetExpr e => PrintLet(e),
            _ => throw new ArgumentOutOfRangeException(nameof(expr), expr.GetType(), "unsupported expression type"),
        };

    private string PrintTuple(TupleExpr e)
    {
        var fieldNames = new string?[e.Items.Count];
        foreach (var pair in e.FieldIndicesByName)
        {
            fieldNames[pair.Value] = pair.Key;
        }
        var items = e.Items.Select((item, i) =>
            fieldNames[i] != null
                ? fieldNames[i] + ": " + Print(item)
                : Print(item));
        return '(' + string.Join(", ", items) + ')';
    }

    private string PrintList(ListExpr e) =>
        '[' + string.Join(", ", e.Items.Select(Print)) + ']';

    private string PrintLet(LetExpr e)
    {
        _indent++;
        var s = $"{Br}{Indent}{Print(e.Let)} in {Print(e.Body)}";
        _indent--;
        return s;
    }

    private string PrintFunction(FuncBinding e)
    {
        _indent++;
        var s = $"let {e.Ident}({PrintParameters(e.Parameters)}) ={Br}{Indent}{Print(e.Body)}";
        _indent--;
        return s;
    }

    private string PrintFrom(FromExpr @from)
    {
        _indent++;
        var clauses = @from.NestedClauses.Count > 0
            ? Br + Indent + string.Join(Br + Indent, @from.NestedClauses.Select(Print))
            : string.Empty;
        var s = Br + Indent
                   + Print(@from.Head)
                   + clauses
                   + Br + Indent
                   + "select " + Print(@from.Selection);
        _indent--;
        return s;
    }

    private static string PrintParameters(IEnumerable<string> idents) =>
        string.Join(", ", idents);

    private string PrintBinary(BinaryExpr expr, string op) =>
        $"{Print(expr.Left)} {op} {Print(expr.Right)}";

    private static string PrintLiteral(Value literal) =>
        literal switch
        {
            StringValue s => '"' + s.Value + '"',
            _ => literal.AsString(),
        };
}
