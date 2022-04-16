﻿using System.Globalization;
using StackExchange.Redis;
using SR = StackExchange.Redis;

namespace RedisQ.Core.Runtime;

public abstract class Value
{
    public abstract string AsString();
    public abstract bool AsBoolean();
}

public class NullValue : Value
{
    private NullValue()
    {}

    public static readonly NullValue Instance = new();

    public override string AsString() => string.Empty;
    public override bool AsBoolean() => false;
}

public abstract class ScalarValue<T> : Value, IEquatable<ScalarValue<T>>
    where T : IEquatable<T>
{
    protected ScalarValue(T value) => Value = value;

    public T Value { get; }

    public bool Equals(ScalarValue<T>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ScalarValue<T>)obj);
    }

    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value);

    public override string ToString() => $"{GetType().Name}[{AsString()}]";
}

public class EnumerableValue : Value, IAsyncEnumerable<Value>
{
    private readonly IAsyncEnumerable<Value> _enumerable;

    public EnumerableValue(IAsyncEnumerable<Value> enumerable) =>
        _enumerable = enumerable;

    public EnumerableValue(IReadOnlyCollection<Value> collection) =>
        _enumerable = AsyncEnumerable.FromCollection(collection);
    
    public IAsyncEnumerator<Value> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        _enumerable.GetAsyncEnumerator(cancellationToken);

    public override string AsString() => throw new RuntimeException("cannot convert enumerable to string");
    public override bool AsBoolean() => throw new RuntimeException("cannot convert enumerable to boolean");
}

public class TupleValue : Value, IEquatable<TupleValue>
{
    public TupleValue(IReadOnlyList<Value> values)
    {
        if (values.Count < 2) throw new ArgumentException("a tuple must have at least two items");
        Values = values;
    }

    public IReadOnlyList<Value> Values { get; }

    public override string AsString() =>
        $"{GetType().Name}[{string.Join(", ", Values.Select(v => v.ToString()))}]";

    public override bool AsBoolean() => true;

    public bool Equals(TupleValue? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Values.Count != other.Values.Count) return false;
        return !Values.Where((t, i) => Equals(t, other.Values[i]) == false).Any();
    }
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TupleValue)obj);
    }

    public override int GetHashCode() =>
        Values.Aggregate(1, (hash, v) => hash * 31 + v.GetHashCode());
}

public interface IRedisKey
{
    RedisKey AsRedisKey();
}

public interface IRedisValue
{
    SR.RedisValue AsRedisValue();
}

public class StringValue : ScalarValue<string>, IRedisKey, IRedisValue
{
    public StringValue(string value) : base(value)
    {}

    public override string AsString() => Value;
    public override bool AsBoolean() => string.IsNullOrEmpty(Value) == false;

    public RedisKey AsRedisKey() => (RedisKey)Value;
    public SR.RedisValue AsRedisValue() => (SR.RedisValue)Value;
}

public class RedisKeyValue : ScalarValue<RedisKey>, IRedisKey, IRedisValue
{
    public RedisKeyValue(RedisKey value) : base(value)
    {}

    public override string AsString() => Value;
    public override bool AsBoolean() => true;

    public RedisKey AsRedisKey() => Value;
    public SR.RedisValue AsRedisValue() => new(Value);
}

public class RedisValue : ScalarValue<SR.RedisValue>, IRedisKey, IRedisValue
{
    public RedisValue(StackExchange.Redis.RedisValue value) : base(value)
    {}

    public override string AsString() => Value;
    public override bool AsBoolean() => Value.HasValue;
    public RedisKey AsRedisKey() => new(Value);
    public SR.RedisValue AsRedisValue() => Value;
}

public class IntegerValue : ScalarValue<int>
{
    public IntegerValue(int value) : base(value)
    {}

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value != 0;
}

public class RealValue : ScalarValue<double>
{
    public RealValue(double value) : base(value)
    {}

    public override string AsString() => Value.ToString(CultureInfo.InvariantCulture);
    public override bool AsBoolean() => Value != 0.0;
}

public class CharValue : ScalarValue<char>
{
    public CharValue(char value) : base(value)
    {}

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value != 0;
}

public class BoolValue : ScalarValue<bool>
{
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    private BoolValue(bool value) : base(value)
    {}

    public static BoolValue Of(bool value) => value ? True : False;

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value;
}