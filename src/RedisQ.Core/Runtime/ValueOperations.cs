namespace RedisQ.Core.Runtime;

internal static class ValueOperations
{
    public static Value Add(Value l, Value r) =>
        (l, r) switch
        {
            (StringValue lv, StringValue rv) => new StringValue(lv.Value + rv.Value),
            (StringValue lv, _) => new StringValue(lv.Value + r.AsString()),
            (_, StringValue rv) => new StringValue(l.AsString() + rv.Value),
            (IntegerValue lv, IntegerValue rv) => IntegerValue.Of(lv.Value + rv.Value),
            (RealValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
            (IntegerValue lv, RealValue rv) => new RealValue(lv.Value + rv.Value),
            (RealValue lv, IntegerValue rv) => new RealValue(lv.Value + rv.Value),
            (NullValue, _) or (_, NullValue) => NullValue.Instance,
            _ => throw new RuntimeException($"the operator '+' cannot be applied to the operands {l} and {r}"),
        };
}

internal class ValueComparer : IComparer<Value>
{
    private ValueComparer()
    {}

    public static readonly IComparer<Value> Default = new ValueComparer();

    public int Compare(Value? l, Value? r)
    {
        if (ReferenceEquals(l, r)) return 0;
        if (ReferenceEquals(l, null)) throw new ArgumentNullException(nameof(l));
        if (ReferenceEquals(r, null)) throw new ArgumentNullException(nameof(r));
        return (l, r) switch
        {
            (StringValue lv, StringValue rv) => string.Compare(lv.Value, rv.Value, StringComparison.Ordinal),
            (IntegerValue lv, IntegerValue rv) => lv.Value.CompareTo(rv.Value),
            (RealValue lv, RealValue rv) => lv.Value.CompareTo(rv.Value),
            (IntegerValue lv, RealValue rv) => ((double) lv.Value).CompareTo(rv.Value),
            (RealValue lv, IntegerValue rv) => lv.Value.CompareTo(rv.Value),
            (IRedisValue lv, IRedisValue rv) => lv.AsRedisValue().CompareTo(rv.AsRedisValue()),
            _ => throw new RuntimeException($"the operands {l} and {r} cannot be compared to each other"),
        };
    }
}
