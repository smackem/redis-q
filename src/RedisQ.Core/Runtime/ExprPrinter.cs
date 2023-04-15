namespace RedisQ.Core.Runtime;

internal class ExprPrinter
{
    private static readonly string Br = Environment.NewLine;

    private int _indent;

    private string Indent => new(' ', 4 * _indent);

    public string Print(Expr expr) => Print(expr, false);

    private string Print(Expr expr, bool needsParens) =>
        expr switch
        {
            ProgramExpr e => string.Join(";" + Br, e.Children.Select(Print)) + ';',
            LiteralExpr e => PrintLiteral(e.Literal),
            TupleExpr e => PrintTuple(e),
            ListExpr e => PrintList(e),
            IdentExpr e => e.Ident,
            FunctionInvocationExpr e => $"{e.Ident}({PrintArguments(e.Arguments)})",
            OrExpr e => PrintBinary(e, "||", needsParens),
            AndExpr e => PrintBinary(e, "&&", needsParens),
            EqExpr e => PrintBinary(e, "==", needsParens),
            NeExpr e => PrintBinary(e, "!=", needsParens),
            MatchExpr e => PrintBinary(e, "~=", needsParens),
            NotMatchExpr e => PrintBinary(e, "!~", needsParens),
            NullCoalescingExpr e => PrintBinary(e, "??", needsParens),
            LtExpr e => PrintBinary(e, "<", needsParens),
            LeExpr e => PrintBinary(e, "<=", needsParens),
            GtExpr e => PrintBinary(e, ">", needsParens),
            GeExpr e => PrintBinary(e, ">=", needsParens),
            RangeExpr e => PrintBinary(e, "..", needsParens),
            PlusExpr e => PrintBinary(e, "+", needsParens),
            MinusExpr e => PrintBinary(e, "-", needsParens),
            TimesExpr e => PrintBinary(e, "*", needsParens),
            DivExpr e => PrintBinary(e, "/", needsParens),
            ModExpr e => PrintBinary(e, "%", needsParens),
            BitAndExpr e => PrintBinary(e, "&", needsParens),
            BitLShiftExpr e => PrintBinary(e, "<<", needsParens),
            BitOrExpr e => PrintBinary(e, "|", needsParens),
            BitRShiftExpr e => PrintBinary(e, ">>", needsParens),
            BitXorExpr e => PrintBinary(e, "^", needsParens),
            BitNotExpr e => "~" + Print(e.Operand, needsParens: true),
            NegExpr e => "-" + Print(e.Operand, needsParens: true),
            PosExpr e => "+" + Print(e.Operand, needsParens: true),
            NotExpr e => "!" + Print(e.Operand, needsParens: true),
            SubscriptExpr e => $"{Print(e.Operand)}[{Print(e.Subscript)}]",
            FieldAccessExpr e => Print(e.Operand) + '.' + e.FieldName,
            TernaryExpr e => needsParens
                ? '(' + Print(e.Condition) + " ? " + Print(e.TrueCase) + " : " + Print(e.FalseCase) + ')'
                : Print(e.Condition) + " ? " + Print(e.TrueCase) + " : " + Print(e.FalseCase),
            FromExpr e => PrintFrom(e),
            EagerFromExpr e => PrintFrom(e.From),
            FromClause e => $"from {e.Ident} in {Print(e.Source)}",
            WhereClause e => $"where {Print(e.Predicate)}",
            WithExpr e => $"{Print(e.Left)} with {Print(e.Right)}",
            LimitClause e => (e.Count, e.Offset) switch
            {
                (not null, null) => $"limit {Print(e.Count)}",
                (null, not null) => $"offset {Print(e.Offset)}",
                (not null, not null) => $"limit {Print(e.Count)} offset {Print(e.Offset)}",
                _ => throw new ArgumentOutOfRangeException()
            },
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
        '[' + PrintArguments(e.Items) + ']';

    private string PrintArguments(IEnumerable<Expr> e) =>
        string.Join(", ", e.Select(Print));

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

    private string PrintBinary(BinaryExpr expr, string op, bool needsParens) =>
        needsParens
        ? $"({Print(expr.Left, true)} {op} {Print(expr.Right, true)})"
        : $"{Print(expr.Left, true)} {op} {Print(expr.Right, true)}";

    private static string PrintLiteral(Value literal) =>
        literal switch
        {
            StringValue s => '"' + s.Value + '"',
            _ => literal.AsString(),
        };
}
