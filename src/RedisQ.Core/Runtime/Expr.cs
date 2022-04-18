using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RedisQ.Core.Runtime;

public abstract record Expr
{
    public async Task<Value> Evaluate(Context ctx)
    {
        try
        {
            return await EvaluateOverride(ctx);
        }
        catch (RuntimeException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new RuntimeException(e);
        }
    }
    
    private protected abstract Task<Value> EvaluateOverride(Context ctx);
}

public record LiteralExpr(Value Literal) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context _) => Task.FromResult(Literal);
}

public record TupleExpr(IReadOnlyList<Expr> Items) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
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

    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var tasks = Items.Select(expr => expr.Evaluate(ctx));
        var list = await Task.WhenAll(tasks);
        return new ListValue(list);
    }
}

public record IdentExpr(string Ident) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx) =>
        Task.FromResult(ctx.Resolve(Ident) ?? throw new RuntimeException($"identifier {Ident} not found"));
}

public record FunctionExpr(string Ident, IReadOnlyList<Expr> Arguments) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var function = ctx.Functions.Resolve(Ident, Arguments.Count);
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

public record OrExpr(Expr Left, Expr Right) : BinaryExpr(Left, Right)
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        BoolValue.Of((await Left.Evaluate(ctx)).AsBoolean() || (await Right.Evaluate(ctx)).AsBoolean());
}

public record AndExpr(Expr Left, Expr Right) : BinaryExpr(Left, Right)
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        BoolValue.Of((await Left.Evaluate(ctx)).AsBoolean() && (await Right.Evaluate(ctx)).AsBoolean());
}

public record SimpleBinaryExpr(Expr Left, Expr Right, Func<Value, Value, Value> EvalFunc)
    : BinaryExpr(Left, Right)
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var l = await Left.Evaluate(ctx).ConfigureAwait(false);
        var r = await Right.Evaluate(ctx).ConfigureAwait(false);
        return EvalFunc(l, r);
    }
}

public record EqExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IRedisKey lv, IRedisKey rv) => BoolValue.Of(lv.AsRedisKey() == rv.AsRedisKey()),
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue() == rv.AsRedisValue()),
        (IRedisValue lv, NullValue) => BoolValue.Of(lv.AsRedisValue().IsNullOrEmpty),
        (NullValue, IRedisValue rv) => BoolValue.Of(rv.AsRedisValue().IsNullOrEmpty),
        _ => BoolValue.Of(Equals(l, r)),
    });

public record NeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IRedisKey lv, IRedisKey rv) => BoolValue.Of(lv.AsRedisKey() != rv.AsRedisKey()),
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue() != rv.AsRedisValue()),
        (IRedisValue lv, NullValue) => BoolValue.Of(lv.AsRedisValue().IsNullOrEmpty == false),
        (NullValue, IRedisValue rv) => BoolValue.Of(rv.AsRedisValue().IsNullOrEmpty == false),
        _ => BoolValue.Of(Equals(l, r) == false),
    });

public record MatchExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => BoolValue.Of(Regex.IsMatch(l.AsString(), r.AsString())),
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
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue().CompareTo(rv.AsRedisValue()) < 0),
        (NullValue, _) or (_, NullValue) => BoolValue.False,
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
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue().CompareTo(rv.AsRedisValue()) <= 0),
        (NullValue, _) or (_, NullValue) => BoolValue.False,
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
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue().CompareTo(rv.AsRedisValue()) > 0),
        (NullValue, _) or (_, NullValue) => BoolValue.False,
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
        (IRedisValue lv, IRedisValue rv) => BoolValue.Of(lv.AsRedisValue().CompareTo(rv.AsRedisValue()) >= 0),
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => throw new RuntimeException($"the operator '>=' cannot be applied to the operands {l} and {r}"),
    });

public record PlusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (StringValue lv, StringValue rv) => new StringValue(lv.Value + rv.Value),
        (StringValue lv, _) => new StringValue(lv.Value + r.AsString()),
        (_, StringValue rv) => new StringValue(l.AsString() + rv.Value),
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value + rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value + rv.Value),
        (CharValue lv, CharValue rv) => IntegerValue.Of(lv.Value + rv.Value),
        (CharValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value + rv.Value),
        (IntegerValue lv, CharValue rv) => IntegerValue.Of(lv.Value + rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '+' cannot be applied to the operands {l} and {r}"),
    });

public record MinusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value - rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value - rv.Value),
        (CharValue lv, CharValue rv) => IntegerValue.Of(lv.Value - rv.Value),
        (CharValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value - rv.Value),
        (IntegerValue lv, CharValue rv) => IntegerValue.Of(lv.Value - rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '-' cannot be applied to the operands {l} and {r}"),
    });

public record TimesExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value * rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value * rv.Value),
        (CharValue lv, CharValue rv) => IntegerValue.Of(lv.Value * rv.Value),
        (CharValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value * rv.Value),
        (IntegerValue lv, CharValue rv) => IntegerValue.Of(lv.Value * rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '*' cannot be applied to the operands {l} and {r}"),
    });

public record DivExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value / rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value / rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value / rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value / rv.Value),
        (CharValue lv, CharValue rv) => IntegerValue.Of(lv.Value / rv.Value),
        (CharValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value / rv.Value),
        (IntegerValue lv, CharValue rv) => IntegerValue.Of(lv.Value / rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '/' cannot be applied to the operands {l} and {r}"),
    });

public record ModExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value % rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value % rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value % rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value % rv.Value),
        (CharValue lv, CharValue rv) => IntegerValue.Of(lv.Value % rv.Value),
        (CharValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value % rv.Value),
        (IntegerValue lv, CharValue rv) => IntegerValue.Of(lv.Value % rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
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

    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        _evalFunc(await Operand.Evaluate(ctx).ConfigureAwait(false));
}

public record NegExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue v => IntegerValue.Of(-v.Value),
        RealValue v => new RealValue(-v.Value),
        CharValue v => IntegerValue.Of(-v.Value),
        NullValue => NullValue.Instance,
        _ => throw new RuntimeException($"operator '-' cannot be applied to operand {value}"),
    });

public record PosExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue => value,
        RealValue => value,
        CharValue => value,
        NullValue => NullValue.Instance,
        _ => throw new RuntimeException($"operator '+' cannot be applied to operand {value}"),
    });

public record NotExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        NullValue => NullValue.Instance,
        _ => BoolValue.Of(value.AsBoolean() == false),
    });

public record SubscriptExpr(Expr Operand, Expr Subscript) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var operandValue = await Operand.Evaluate(ctx).ConfigureAwait(false);
        var subscriptValue = await Subscript.Evaluate(ctx).ConfigureAwait(false);
        return (operandValue, subscriptValue) switch
        {
            (ListValue list, IntegerValue index) => list[CoerceIndex(list, index.Value)],
            (StringValue str, IntegerValue index) => new CharValue(str.Value[CoerceIndex(str.Value, index.Value)]),
            (StringValue json, StringValue path) => JsonPath.Select(json.AsString(), path.Value),
            (RedisValue json, StringValue path) => JsonPath.Select(json.AsString(), path.Value),
            (NullValue, _) or (_, NullValue) => NullValue.Instance,
            _ => throw new RuntimeException($"incompatible operands for subscript expression: {operandValue}[{subscriptValue}]"),
        };
    }

    private static int CoerceIndex(IReadOnlyCollection<Value> list, int index) =>
        index < 0 ? list.Count + index : index;

    private static int CoerceIndex(string str, int index) =>
        index < 0 ? str.Length + index : index;
}

public record TernaryExpr(Expr Condition, Expr TrueCase, Expr FalseCase) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        (await Condition.Evaluate(ctx)).AsBoolean()
            ? await TrueCase.Evaluate(ctx)
            : await FalseCase.Evaluate(ctx);
}

public record FromExpr(FromClause Head, IReadOnlyList<NestedClause> NestedClauses, Expr Selection) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx)
    {
        ctx = Context.Inherit(ctx);
        var selection = SelectFromSource(Head, ctx);

        foreach (var clause in NestedClauses)
        {
            selection = clause switch
            {
                FromClause @from => CrossJoin(selection, @from, ctx),
                LetClause @let => Bind(selection, @let, ctx),
                WhereClause @where => Filter(selection, @where, ctx),
                _ => throw new NotImplementedException(),
            };
        }

        async IAsyncEnumerable<Value> OuterSelect(IAsyncEnumerable<Value> inner)
        {
            await foreach (var _ in inner.ConfigureAwait(false))
            {
                var value = await Selection.Evaluate(ctx).ConfigureAwait(false);
                Trace.WriteLine($"SelectSource: {value}");
                yield return value;
            }
        }

        var resultValue = new EnumerableValue(OuterSelect(selection));
        return Task.FromResult(resultValue as Value);
    }

    private static async IAsyncEnumerable<Value> SelectFromSource(FromClause @from,  Context ctx)
    {
        var sourceValue = await @from.Source.Evaluate(ctx);
        if (sourceValue is EnumerableValue source == false) throw new RuntimeException($"from: source value {sourceValue} is not enumerable");

        await foreach (var value in source.ConfigureAwait(false))
        {
            Trace.WriteLine($"SelectSource: {value}");
            ctx.Bind(from.Ident, value);
            yield return value;
        }
    }

    private static async IAsyncEnumerable<Value> CrossJoin(IAsyncEnumerable<Value> coll, FromClause @from, Context ctx)
    {
        await foreach (var _ in coll.ConfigureAwait(false))
        {
            var selection = SelectFromSource(@from, ctx);
            await foreach (var value in selection.ConfigureAwait(false))
            {
                yield return value;
            }
        }
    }

    private static async IAsyncEnumerable<Value> Filter(IAsyncEnumerable<Value> coll, WhereClause @where, Context ctx)
    {
        await foreach (var value in coll.ConfigureAwait(false))
        {
            var include = await @where.Predicate.Evaluate(ctx).ConfigureAwait(false);
            if (include.AsBoolean()) yield return value;
        }
    }

    private static async IAsyncEnumerable<Value> Bind(IAsyncEnumerable<Value> coll, LetClause @let, Context ctx)
    {
        await foreach (var value in coll.ConfigureAwait(false))
        {
            _ = await @let.Evaluate(ctx).ConfigureAwait(false);
            yield return value;
        }
    }
}

public record EagerFromExpr(FromExpr From) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var enumerable = (EnumerableValue) await From.Evaluate(ctx).ConfigureAwait(false);
        var collection = await enumerable.Collect().ConfigureAwait(false);
        return new ListValue(collection);
    }
}

public abstract record NestedClause : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx) => throw new NotSupportedException();
}

public record FromClause(string Ident, Expr Source) : NestedClause;
public record WhereClause(Expr Predicate) : NestedClause;

public record LetClause(string Ident, Expr Right) : NestedClause
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var value = await Right.Evaluate(ctx).ConfigureAwait(false);
        ctx.Bind(Ident, value);
        return value;
    }
}

public record ThrowExpr(Expr Exception) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var exception = await Exception.Evaluate(ctx).ConfigureAwait(false);
        throw new RuntimeException($"Runtime exception: {exception.AsString()}");
    }
}