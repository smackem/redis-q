using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RedisQ.Core.Runtime;

public abstract record Expr
{
    public async Task<Value> Evaluate(Context ctx)
    {
        try
        {
            return await EvaluateOverride(ctx).ConfigureAwait(false);
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

    public string Print() => new ExprPrinter().Print(this);

    private protected abstract Task<Value> EvaluateOverride(Context ctx);
}

public record ProgramExpr(IReadOnlyList<Expr> Children) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var values = new List<Value>();
        foreach (var expr in Children)
        {
            var value = await expr.Evaluate(ctx).ConfigureAwait(false);
            values.Add(value);
        }

        return new ListValue(values);
    }
}

public record LiteralExpr(Value Literal) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context _) => Task.FromResult(Literal);
}

public record TupleExpr(IReadOnlyList<Expr> Items, IReadOnlyDictionary<string, int> FieldIndicesByName) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var values = new List<Value>();
        foreach (var expr in Items)
        {
            var value = await expr.Evaluate(ctx).ConfigureAwait(false);
            values.Add(value);
        }
        return new TupleValue(values, FieldIndicesByName);
    }
}

public record ListExpr(IReadOnlyList<Expr> Items) : Expr
{
    public static readonly ListExpr Empty = new(Array.Empty<Expr>());

    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var list = new List<Value>();
        foreach (var item in Items)
        {
            var value = await item.Evaluate(ctx).ConfigureAwait(false);
            list.Add(value);
        }
        return new ListValue(list);
    }
}

public record IdentExpr(string Ident) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx) =>
        Task.FromResult(ctx.Resolve(Ident) ?? throw new RuntimeException($"identifier {Ident} not found"));
}

public record FunctionInvocationExpr(string Ident, IReadOnlyList<Expr> Arguments) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx) =>
        ctx.Resolve(Ident) is FunctionValue funcVal
            ? InvokeFunctionValue(funcVal, ctx)
            : InvokeBuiltInFunction(ctx);

    private async Task<Value> InvokeFunctionValue(FunctionValue func, Context ctx)
    {
        var funcCtx = Context.Inherit(ctx);
        var argCount = Arguments.Count;
        if (func.Parameters.Count != argCount) throw new RuntimeException($"function '{func.AsString()}' expects {func.Parameters.Count} arguments but got {Arguments.Count}");
        var arguments = await EvalArguments(ctx).ConfigureAwait(false);
        for (var i = 0; i < argCount; i++)
        {
            funcCtx.Bind(func.Parameters[i], arguments[i]);
        }
        return await func.Body.Evaluate(funcCtx).ConfigureAwait(false);
    }

    private async Task<Value> InvokeBuiltInFunction(Context ctx)
    {
        var function = ctx.Functions.Resolve(Ident, Arguments.Count);
        var arguments = await EvalArguments(ctx).ConfigureAwait(false);
        return await function.Invoke(ctx, arguments.ToArray()).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Value>> EvalArguments(Context ctx)
    {
        var arguments = new List<Value>();
        foreach (var arg in Arguments)
        {
            var value = await arg.Evaluate(ctx).ConfigureAwait(false);
            arguments.Add(value);
        }
        return arguments;
    }
}

public abstract record BinaryExpr(Expr Left, Expr Right) : Expr;

public record OrExpr(Expr Left, Expr Right) : BinaryExpr(Left, Right)
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        BoolValue.Of(
            (await Left.Evaluate(ctx).ConfigureAwait(false)).AsBoolean() ||
            (await Right.Evaluate(ctx).ConfigureAwait(false)).AsBoolean());
}

public record AndExpr(Expr Left, Expr Right) : BinaryExpr(Left, Right)
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        BoolValue.Of(
            (await Left.Evaluate(ctx).ConfigureAwait(false)).AsBoolean() &&
            (await Right.Evaluate(ctx).ConfigureAwait(false)).AsBoolean());
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
        _ => BoolValue.Of(Regex.IsMatch(l.AsString(), r.AsString(), RegexOptions.IgnoreCase)),
    });

public record NotMatchExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.True,
        _ => BoolValue.Of(!Regex.IsMatch(l.AsString(), r.AsString(), RegexOptions.IgnoreCase)),
    });

public record NullCoalescingExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    l is NullValue ? r : l);

public record LtExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => BoolValue.Of(ValueComparer.Default.Compare(l, r) < 0),
    });

public record LeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => BoolValue.Of(ValueComparer.Default.Compare(l, r) <= 0),
    });

public record GtExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => BoolValue.Of(ValueComparer.Default.Compare(l, r) > 0),
    });

public record GeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (NullValue, _) or (_, NullValue) => BoolValue.False,
        _ => BoolValue.Of(ValueComparer.Default.Compare(l, r) >= 0),
    });

public record RangeExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => new RangeValue(lv.Value, rv.Value),
        (RealValue lv, IntegerValue rv) => new RangeValue((long) lv.Value, rv.Value),
        (IntegerValue lv, RealValue rv) => new RangeValue(lv.Value, (long) rv.Value),
        (RealValue lv, RealValue rv) => new RangeValue((long) lv.Value, (long) rv.Value),
        _ => throw new RuntimeException($"the operator '..' can only be applied to integers, not to {l} and {r}"),
    });

public record WithExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (TupleValue lv, TupleValue rv) => TupleValue.Combine(lv, rv), 
        _ => throw new RuntimeException($"the operator 'with' can only be applied to tuples, not to {l} and {r}"),
    });

public record PlusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, ValueOperations.Add);

public record MinusExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value - rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value - rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value - rv.Value),
        (TimestampValue lv, TimestampValue rv) => new DurationValue(lv.Value - rv.Value),
        (TimestampValue lv, DurationValue rv) => new TimestampValue(lv.Value - rv.Value),
        (DurationValue lv, DurationValue rv) => new DurationValue(lv.Value - rv.Value),
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '-' cannot be applied to the operands {l} and {r}"),
    });

public record BitAndExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value & rv.Value),
        _ => throw new RuntimeException($"the operator '&' cannot be applied to the operands {l} and {r}"),
    });

public record BitOrExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value | rv.Value),
        _ => throw new RuntimeException($"the operator '&' cannot be applied to the operands {l} and {r}"),
    });

public record BitXorExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value ^ rv.Value),
        _ => throw new RuntimeException($"the operator '^' cannot be applied to the operands {l} and {r}"),
    });

public record TimesExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value * rv.Value),
        (RealValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (IntegerValue lv, RealValue rv) => new RealValue(lv.Value * rv.Value),
        (RealValue lv, IntegerValue rv) => new RealValue(lv.Value * rv.Value),
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
        (NullValue, _) or (_, NullValue) => NullValue.Instance,
        _ => throw new RuntimeException($"the operator '%' cannot be applied to the operands {l} and {r}"),
    });

public record BitLShiftExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value << (int) rv.Value),
        _ => throw new RuntimeException($"the operator '<<' cannot be applied to the operands {l} and {r}"),
    });

public record BitRShiftExpr(Expr Left, Expr Right) : SimpleBinaryExpr(Left, Right, (l, r) =>
    (l, r) switch
    {
        (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value >> (int) rv.Value),
        _ => throw new RuntimeException($"the operator '>>' cannot be applied to the operands {l} and {r}"),
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
        NullValue => NullValue.Instance,
        _ => throw new RuntimeException($"operator '-' cannot be applied to operand {value}"),
    });

public record PosExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue => value,
        RealValue => value,
        NullValue => NullValue.Instance,
        _ => throw new RuntimeException($"operator '+' cannot be applied to operand {value}"),
    });

public record NotExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        NullValue => NullValue.Instance,
        _ => BoolValue.Of(value.AsBoolean() == false),
    });

public record BitNotExpr(Expr Operand) : UnaryExpr(Operand, value =>
    value switch
    {
        IntegerValue n => IntegerValue.Of(~n.Value),
        _ => throw new RuntimeException($"operator '~' cannot be applied to operand {value}"),
    });

public record SubscriptExpr(Expr Operand, Expr Subscript) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var operandValue = await Operand.Evaluate(ctx).ConfigureAwait(false);
        var subscriptValue = await Subscript.Evaluate(ctx).ConfigureAwait(false);
        return (operandValue, subscriptValue) switch
        {
            (ListValue list, IntegerValue index) => list[CoerceIndex(list.Count, (int) index.Value)],
            (ListValue list, RangeValue range) => Slice(list, range),
            (TupleValue tuple, IntegerValue index) => tuple.Items[CoerceIndex(tuple.Items.Count, (int) index.Value)],
            (StringValue str, IntegerValue index) => new StringValue(str.Value[CoerceIndex(str.Value.Length, (int) index.Value)].ToString()),
            (StringValue str, RangeValue range) => Slice(str, range),
            (StringValue json, StringValue path) => JsonPath.Select(json.AsString(), path.Value),
            (RedisValue json, StringValue path) => JsonPath.Select(json.AsString(), path.Value),
            (NullValue, _) or (_, NullValue) => NullValue.Instance,
            _ => throw new RuntimeException($"incompatible operands for subscript expression: {operandValue}[{subscriptValue}]"),
        };
    }

    private static ListValue Slice(ListValue list, RangeValue value)
    {
        var newRange = CoerceRange(list.Count, (int) value.Lower, (int) value.Upper);
        return newRange == null
            ? ListValue.Empty
            : list.Slice(newRange.Value.Item1, newRange.Value.Item2);
    }

    private static StringValue Slice(StringValue str, RangeValue value)
    {
        var newRange = CoerceRange(str.Value.Length, (int) value.Lower, (int) value.Upper);
        return newRange == null
            ? StringValue.Empty
            : str.Slice(newRange.Value.Item1, newRange.Value.Item2);
    }

    private static (int, int)? CoerceRange(int length, int lower, int upper)
    {
        lower = CoerceIndex(length, lower);
        upper = CoerceIndex(length, upper);
        if (lower < 0 && upper < 0) return null;
        if (lower >= length) return null;
        if (lower < 0) lower = 0;
        if (upper > length - 1) upper = length - 1;
        return (lower, upper);
    }

    private static int CoerceIndex(int length, int index)
    {
        index = index < 0 ? length + index : index;
        return index;
    }
}

public record FieldAccessExpr(Expr Operand, string FieldName) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var operandValue = await Operand.Evaluate(ctx).ConfigureAwait(false);
        return operandValue switch
        {
            TupleValue tuple => tuple[FieldName],
            _ => throw new RuntimeException($"incompatible operands for field access expression: {operandValue}[{FieldName}]"),
        };
    }
}

public record TernaryExpr(Expr Condition, Expr TrueCase, Expr FalseCase) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx) =>
        (await Condition.Evaluate(ctx).ConfigureAwait(false)).AsBoolean()
            ? await TrueCase.Evaluate(ctx).ConfigureAwait(false)
            : await FalseCase.Evaluate(ctx).ConfigureAwait(false);
}

public record FromExpr(FromClause Head, IReadOnlyList<NestedClause> NestedClauses, Expr Selection) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx)
    {
        ctx = Context.Inherit(ctx);
        var selection = SelectFromSource(Head, ctx);

        async IAsyncEnumerable<Value> OuterSelect(IAsyncEnumerable<Value> inner)
        {
            await foreach (var _ in inner.ConfigureAwait(false))
            {
                var value = await Selection.Evaluate(ctx).ConfigureAwait(false);
                Trace.WriteLine($"SelectSource: {value}");
                yield return value;
            }
        }

        foreach (var clause in NestedClauses)
        {
            selection = clause switch
            {
                FromClause @from => CrossJoin(selection, @from, ctx),
                LetClause @let => Bind(selection, @let, ctx),
                WhereClause @where => Filter(selection, @where, ctx),
                LimitClause limit => Limit(selection, limit, ctx),
                OrderByClause orderBy => OrderBy(selection, orderBy, ctx),
                GroupByClause groupBy => GroupBy(selection, groupBy, ctx),
                _ => throw new NotImplementedException(),
            };
        }

        var resultValue = new EnumerableValue(OuterSelect(selection));
        return Task.FromResult(resultValue as Value);
    }

    private static async IAsyncEnumerable<Value> SelectFromSource(FromClause @from, Context ctx)
    {
        var sourceValue = await @from.Source.Evaluate(ctx).ConfigureAwait(false);
        if (sourceValue is EnumerableValue source == false) throw new RuntimeException($"from: source value {sourceValue} is not enumerable");

        await foreach (var value in SelectFromSource(source, from.Ident, ctx).ConfigureAwait(false))
        {
            yield return value;
        }
    }

    private static async IAsyncEnumerable<Value> SelectFromSource(IAsyncEnumerable<Value> @from, string ident, Context ctx)
    {
        await foreach (var value in @from.ConfigureAwait(false))
        {
            ctx.Bind(ident, value);
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

    private static async IAsyncEnumerable<Value> Limit(IAsyncEnumerable<Value> coll, LimitClause limit, Context ctx)
    {
        var countValue = limit.Count != null
            ? await limit.Count.Evaluate(ctx).ConfigureAwait(false)
            : NullValue.Instance;
        long? count = countValue switch
        {
            NullValue => null,
            IntegerValue n => n.Value,
            _ => throw new RuntimeException("limit clause expects integer argument or null"),
        };

        var offsetValue = limit.Offset != null
            ? await limit.Offset.Evaluate(ctx).ConfigureAwait(false)
            : IntegerValue.Zero;
        var first = offsetValue is IntegerValue o
            ? o.Value
            : throw new RuntimeException("limit clause expected integer arguments");

        var last = first + count;
        var index = 0;
        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (index >= last) break;
            if (index >= first) yield return value;
            index++;
        }
    }

    private static async IAsyncEnumerable<Value> OrderBy(IAsyncEnumerable<Value> coll, OrderByClause orderBy, Context ctx)
    {
        // we need to capture a closure for each element because we change the order of elements,
        // so when replaying the ordered collection we can reassemble the correct context
        var keysAndValues = new List<(Value key, Value value, Scope closure)>();
        await foreach (var value in coll.ConfigureAwait(false))
        {
            var key = await orderBy.Key.Evaluate(ctx).ConfigureAwait(false);
            keysAndValues.Add((key, value, ctx.CaptureClosure()));
        }
        var ordered = orderBy.IsDescending
            ? keysAndValues.OrderByDescending(tuple => tuple.key, ValueComparer.Default)
            : keysAndValues.OrderBy(tuple => tuple.key, ValueComparer.Default);
        foreach (var (_, value, closure) in ordered)
        {
            ctx.BindAll(closure);
            yield return value;
        }
    }

    private record Group(Scope Closure, List<Value> Elements);

    private static async IAsyncEnumerable<Value> GroupBy(IAsyncEnumerable<Value> coll, GroupByClause groupBy, Context ctx)
    {
        var groups = new Dictionary<Value, Group>();
        await foreach (var _ in coll.ConfigureAwait(false))
        {
            var key = await groupBy.Key.Evaluate(ctx).ConfigureAwait(false);
            if (groups.TryGetValue(key, out var group) == false)
            {
                group = new Group(ctx.CaptureClosure(), new List<Value>());
                groups.Add(key, group);
            }
            var element = await groupBy.Value.Evaluate(ctx).ConfigureAwait(false);
            group.Elements.Add(element);
        }
        foreach (var pair in groups)
        {
            ctx.BindAll(pair.Value.Closure);
            var groupValue = TupleValue.Of(
                ("key", pair.Key),
                ("values", new ListValue(pair.Value.Elements)));
            ctx.Bind(groupBy.Ident, groupValue);
            yield return pair.Key;
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
public record LimitClause(Expr? Count, Expr? Offset) : NestedClause;
public record OrderByClause(Expr Key, bool IsDescending) : NestedClause;
public record GroupByClause(Expr Value, Expr Key, string Ident) : NestedClause;

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

public record FuncBinding(string Ident, IReadOnlyList<string> Parameters, Expr Body) : Expr
{
    private protected override Task<Value> EvaluateOverride(Context ctx)
    {
        var value = new FunctionValue(Ident, Parameters, Body);
        ctx.Bind(Ident, value);
        return Task.FromResult<Value>(value);
    }
}

public record LetExpr(LetClause Let, Expr Body) : Expr
{
    private protected override async Task<Value> EvaluateOverride(Context ctx)
    {
        var bodyCtx = Context.Inherit(ctx);
        await Let.Evaluate(bodyCtx).ConfigureAwait(false);
        return await Body.Evaluate(bodyCtx).ConfigureAwait(false);
    }
}