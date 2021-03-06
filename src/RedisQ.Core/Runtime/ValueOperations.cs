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
            (TimestampValue lv, DurationValue rv) => new TimestampValue(lv.Value + rv.Value),
            (DurationValue lv, DurationValue rv) => new DurationValue(lv.Value + rv.Value),
            (NullValue, _) or (_, NullValue) => NullValue.Instance,
            _ => throw new RuntimeException($"the operator '+' cannot be applied to the operands {l} and {r}"),
        };
}

internal static class ValueCollections
{
    public static bool Equal(IReadOnlyList<Value> l, IReadOnlyList<Value> r)
    {
        if (ReferenceEquals(l, r)) return true;
        if (l.Count != r.Count) return false;
        return !l.Where((t, i) => Equals(t, r[i]) == false).Any();
    }

    public static int GetHashCode(IEnumerable<Value> coll) =>
        coll.Aggregate(1, (hash, v) => hash * 31 + v.GetHashCode());
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
            (TimestampValue lv, TimestampValue rv) => lv.Value.CompareTo(rv.Value),
            (DurationValue lv, DurationValue rv) => lv.Value.CompareTo(rv.Value),
            (IRedisValue lv, IRedisValue rv) => lv.AsRedisValue().CompareTo(rv.AsRedisValue()),
            (NullValue, NullValue) => 0,
            (NullValue, _) => -1,
            (_, NullValue) => 1,
            _ => throw new RuntimeException($"the operands {l} and {r} cannot be compared to each other"),
        };
    }
}
