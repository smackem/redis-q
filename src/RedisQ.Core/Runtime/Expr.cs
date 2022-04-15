using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RedisQ.Core.Runtime;

public abstract record Expr
{
    public abstract Task<Value> Evaluate(Context ctx);
}

public record LiteralExpr(Value Literal) : Expr
{
    public override Task<Value> Evaluate(Context _) => Task.FromResult(Literal);
}

public record TupleExpr(IReadOnlyList<Expr> Items) : Expr
{
    public override async Task<Value> Evaluate(Context ctx)
    {
        var values = new List<Value>();
        foreach (var expr in Items)
        {
            var value = await expr.Evaluate(ctx).ConfigureAwait(false);
            values.Add(value);
        }
        return new TupleValue(values);
    }
}

public record ListExpr(IReadOnlyList<Expr> Items) : Expr
{
    public static readonly ListExpr Empty = new(Array.Empty<Expr>());

    public override Task<Value> Evaluate(Context ctx)
    {
        async IAsyncEnumerable<Value> Enumerate()
        {
            foreach (var expr in Items)
            {
                yield return await expr.Evaluate(ctx);
            }
        }

        return Task.FromResult(new EnumerableValue(Enumerate()) as Value);
    }
}

public record IdentExpr(string Ident) : Expr
{
    public override Task<Value> Evaluate(Context ctx) =>
        Task.FromResult(ctx.LookupBinding(Ident) ?? throw new RuntimeException($"identifier {Ident} not found"));
}

public record FunctionExpr(string Ident, IReadOnlyList<Expr> Arguments) : Expr
{
    public override async Task<Value> Evaluate(Context ctx)
    {
        var function = ctx.Functions.Lookup(Ident, Arguments.Count);
        var arguments = new List<Value>();
        foreach (var arg in Arguments)
        {
            var value = await arg.Evaluate(ctx).ConfigureAwait(false);
            arguments.Add(value);
        }

        return await function.Invoke(ctx, arguments.ToArray()).ConfigureAwait(false);
    }
}

public abstract record BinaryExpr(Expr Left, Expr Right) : Expr;

public record SimpleBinaryExpr : BinaryExpr
{
    private readonly Func<Value, Value, Value> _evalFunc;

    protected SimpleBinaryExpr(Expr left, Expr right, Func<Value, Value, Value> evalFunc)
        : base(left, right) => _evalFunc = evalFunc;

    public override async Task<Value> Evaluate(Context ctx)
    {
        var l = await Left.Evaluate(ctx).ConfigureAwait(false);
        var r = await Right.Evaluate(ctx).ConfigureAwait(false);
        return _evalFunc(l, r);
    }
}

public record OrExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    BoolValue.Of(l.AsBoolean() || r.AsBoolean()));

public record AndExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    BoolValue.Of(l.AsBoolean() && r.AsBoolean()));

public record EqExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    BoolValue.Of(Equals(l, r)));

public record NeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    BoolValue.Of(Equals(l, r) == false));

public record MatchExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => BoolValue.Of(Regex.IsMatch(lv.Value, rv.Value)),
        _ => throw new RuntimeException($"incompatible values for match operator. expected (string, string) but found ({l}, {r}"),
    });

public record LtExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => BoolValue.Of(string.Compare(lv.Value, rv.Value, StringComparison.Ordinal) < 0),
        (IntegerValue lv, IntegerValue rv) => BoolValue.Of(lv.Value < rv.Value),
        (RealValue lv, RealValue rv) => BoolValue.Of(lv.Value < rv.Value),
        (IntegerValue lv, RealValue rv) => BoolValue.Of(lv.Value < rv.Value),
        (RealValue lv, IntegerValue rv) => BoolValue.Of(lv.Value < rv.Value),
        (CharValue lv, CharValue rv) => BoolValue.Of(lv.Value < rv.Value),
        _ => throw new RuntimeException($"the operator '<' cannot be applied to the operands {l} and {r}"),
    });

public record LeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => BoolValue.Of(string.Compare(lv.Value, rv.Value, StringComparison.Ordinal) <= 0),
        (IntegerValue lv, IntegerValue rv) => BoolValue.Of(lv.Value <= rv.Value),
        (RealValue lv, RealValue rv) => BoolValue.Of(lv.Value <= rv.Value),
        (IntegerValue lv, RealValue rv) => BoolValue.Of(lv.Value <= rv.Value),
        (RealValue lv, IntegerValue rv) => BoolValue.Of(lv.Value <= rv.Value),
        (CharValue lv, CharValue rv) => BoolValue.Of(lv.Value <= rv.Value),
        _ => throw new RuntimeException($"the operator '<=' cannot be applied to the operands {l} and {r}"),
    });

public record GtExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => BoolValue.Of(string.Compare(lv.Value, rv.Value, StringComparison.Ordinal) > 0),
        (IntegerValue lv, IntegerValue rv) => BoolValue.Of(lv.Value > rv.Value),
        (RealValue lv, RealValue rv) => BoolValue.Of(lv.Value > rv.Value),
        (IntegerValue lv, RealValue rv) => BoolValue.Of(lv.Value > rv.Value),
        (RealValue lv, IntegerValue rv) => BoolValue.Of(lv.Value > rv.Value),
        (CharValue lv, CharValue rv) => BoolValue.Of(lv.Value > rv.Value),
        _ => throw new RuntimeException($"the operator '>' cannot be applied to the operands {l} and {r}"),
    });

public record GeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => BoolValue.Of(string.Compare(lv.Value, rv.Value, StringComparison.Ordinal) >= 0),
        (IntegerValue lv, IntegerValue rv) => BoolValue.Of(lv.Value >= rv.Value),
        (RealValue lv, RealValue rv) => BoolValue.Of(lv.Value >= rv.Value),
        (IntegerValue lv, RealValue rv) => BoolValue.Of(lv.Value >= rv.Value),
        (RealValue lv, IntegerValue rv) => BoolValue.Of(lv.Value >= rv.Value),
        (CharValue lv, CharValue rv) => BoolValue.Of(lv.Value >= rv.Value),
        _ => throw new RuntimeException($"the operator '>=' cannot be applied to the operands {l} and {r}"),
    });

public record PlusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => new StringValue(lv.Value + rv.Value),
        (StringValue lv, _) => new StringValue(lv.Value + r.AsString()),
        (_, StringValue rv) => new StringValue(l.AsString() + rv.Value),
        (IntegerValue lv, IntegerValue rv) => new IntegerValue(lv.Value + rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value + rv.Value),
        (CharValue lv, CharValue rv) => new IntegerValue(lv.Value + rv.Value),
        (CharValue lv, IntegerValue rv) => new IntegerValue(lv.Value + rv.Value),
        (IntegerValue lv, CharValue rv) => new IntegerValue(lv.Value + rv.Value),
        _ => throw new RuntimeException($"the operator '+' cannot be applied to the operands {l} and {r}"),
    });

public record MinusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => new IntegerValue(lv.Value - rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value - rv.Value),
        (CharValue lv, CharValue rv) => new IntegerValue(lv.Value - rv.Value),
        (CharValue lv, IntegerValue rv) => new IntegerValue(lv.Value - rv.Value),
        (IntegerValue lv, CharValue rv) => new IntegerValue(lv.Value - rv.Value),
        _ => throw new RuntimeException($"the operator '-' cannot be applied to the operands {l} and {r}"),
    });

public record TimesExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => new IntegerValue(lv.Value * rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value * rv.Value),
        (CharValue lv, CharValue rv) => new IntegerValue(lv.Value * rv.Value),
        (CharValue lv, IntegerValue rv) => new IntegerValue(lv.Value * rv.Value),
        (IntegerValue lv, CharValue rv) => new IntegerValue(lv.Value * rv.Value),
        _ => throw new RuntimeException($"the operator '*' cannot be applied to the operands {l} and {r}"),
    });

public record DivExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => new IntegerValue(lv.Value / rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value / rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value / rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value / rv.Value),
        (CharValue lv, CharValue rv) => new IntegerValue(lv.Value / rv.Value),
        (CharValue lv, IntegerValue rv) => new IntegerValue(lv.Value / rv.Value),
        (IntegerValue lv, CharValue rv) => new IntegerValue(lv.Value / rv.Value),
        _ => throw new RuntimeException($"the operator '/' cannot be applied to the operands {l} and {r}"),
    });

public record ModExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => new IntegerValue(lv.Value % rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value % rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value % rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value % rv.Value),
        (CharValue lv, CharValue rv) => new IntegerValue(lv.Value % rv.Value),
        (CharValue lv, IntegerValue rv) => new IntegerValue(lv.Value % rv.Value),
        (IntegerValue lv, CharValue rv) => new IntegerValue(lv.Value % rv.Value),
        _ => throw new RuntimeException($"the operator '%' cannot be applied to the operands {l} and {r}"),
    });

public record UnaryExpr : Expr
{
    private readonly Func<Value, Value> _evalFunc;

    protected UnaryExpr(Expr operand, Func<Value, Value> evalFunc)
    {
        Operand = operand;
        _evalFunc = evalFunc;
    }

    public Expr Operand { get; }

    public override async Task<Value> Evaluate(Context ctx) =>
        _evalFunc(await Operand.Evaluate(ctx).ConfigureAwait(false));
}

public record NegExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue v => new IntegerValue(-v.Value),
        RealValue v => new RealValue(-v.Value),
        CharValue v => new IntegerValue(-v.Value),
        _ => throw new RuntimeException($"operator '-' cannot be applied to operand {value}"),
    });

public record PosExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue _ => value,
        RealValue _ => value,
        CharValue _ => value,
        _ => throw new RuntimeException($"operator '+' cannot be applied to operand {value}"),
    });

public record NotExpr(Expr Operand) : UnaryExpr(Operand, value =>
    BoolValue.Of(value.AsBoolean() == false));

public record FromExpr(FromClause Head, IReadOnlyList<NestedClause> NestedClauses, Expr Selection) : Expr
{
    public override async Task<Value> Evaluate(Context ctx)
    {
        ctx = Context.Inherit(ctx);
        var source = await Head.Source.Evaluate(ctx);
        if (source is EnumerableValue coll == false) throw new RuntimeException($"from: source value {source} is not enumerable");

        async IAsyncEnumerable<Value> InnerSelect()
        {
            await foreach (var value in coll)
            {
                Trace.WriteLine($"InnerSelect: {value}");
                ctx.Bind(Head.Ident, value);
                yield return value;
            }
        }

        var selection = InnerSelect();
        foreach (var clause in NestedClauses)
        {
            selection = clause switch
            {
                LetClause let => Bind(selection, let, ctx),
                WhereClause where => Filter(selection, where, ctx),
                _ => throw new NotImplementedException(),
            };
        }

        async IAsyncEnumerable<Value> OuterSelect(IAsyncEnumerable<Value> inner)
        {
            await foreach (var _ in inner)
            {
                var value = await Selection.Evaluate(ctx);
                Trace.WriteLine($"InnerSelect: {value}");
                yield return value;
            }
        }

        return new EnumerableValue(OuterSelect(selection));
    }

    private static async IAsyncEnumerable<Value> Filter(IAsyncEnumerable<Value> coll, WhereClause where, Context ctx)
    {
        await foreach (var value in coll.ConfigureAwait(false))
        {
            var include = await where.Predicate.Evaluate(ctx).ConfigureAwait(false);
            if (include.AsBoolean()) yield return value;
        }
    }

    private static async IAsyncEnumerable<Value> Bind(IAsyncEnumerable<Value> coll, LetClause let, Context ctx)
    {
        await foreach (var value in coll.ConfigureAwait(false))
        {
            var r = await let.Right.Evaluate(ctx).ConfigureAwait(false);
            ctx.Bind(let.Ident, r);
            yield return value;
        }
    }
}

public abstract record NestedClause : Expr
{
    public override Task<Value> Evaluate(Context ctx) => throw new NotSupportedException();
}
public record FromClause(string Ident, Expr Source) : NestedClause;
public record LetClause(string Ident, Expr Right) : NestedClause;
public record WhereClause(Expr Predicate) : NestedClause;
