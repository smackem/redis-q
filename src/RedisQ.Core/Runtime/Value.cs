using System.Collections;
using System.Globalization;
using System.Text;
using StackExchange.Redis;
using SR = StackExchange.Redis;

namespace RedisQ.Core.Runtime;

public abstract class Value
{
    public abstract string AsString();
    public abstract bool AsBoolean();

    public string TypeName =>
        GetType().Name.ToLower() switch
        {
            var s when s.EndsWith("value") => s[..^5],
            var s => s,
        };
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

    public IAsyncEnumerator<Value> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        _enumerable.GetAsyncEnumerator(cancellationToken);

    public override string AsString() => throw new RuntimeException("cannot convert enumerable to string");
    public override bool AsBoolean() => throw new RuntimeException("cannot convert enumerable to boolean");
}

public class ListValue : EnumerableValue, IReadOnlyList<Value>, IEquatable<ListValue>
{
    private readonly IReadOnlyList<Value> _list;

    public static readonly ListValue Empty = new(Array.Empty<Value>()); 

    public ListValue(IReadOnlyList<Value> collection)
        : base(AsyncEnumerable.FromCollection(collection)) =>
        _list = collection;

    public Value this[int index] => _list[index];

    public int Count => _list.Count;

    public static ListValue Of(params Value[] values) => new(values);

    public ListValue Slice(int lower, int upper) => new(_list.ToArray()[lower .. (upper + 1)]);

    public int? IndexOf(Value value)
    {
        var count = _list.Count;
        for (var i = 0; i < count; i++)
        {
            if (Equals(_list[i], value)) return i;
        }

        return null;
    }

    public IEnumerator<Value> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        const int max = 16;
        var sb = new StringBuilder()
            .Append(GetType().Name)
            .Append($"(Size={Count})")
            .Append('[');
        var items = _list.Take(max);
        sb.Append(string.Join(", ", items));
        if (_list.Count > max) sb.Append(", ...");
        return sb.Append(']').ToString();
    }

    public override bool AsBoolean() => Count > 0;
    
    public bool Equals(ListValue? other) =>
        ReferenceEquals(null, other) == false
        && ValueCollections.Equal(_list, other._list);
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ListValue)obj);
    }

    public override int GetHashCode() =>
        ValueCollections.GetHashCode(_list);
}

public class TupleValue : Value, IEquatable<TupleValue>
{
    private static readonly IReadOnlyDictionary<string, int> EmptyFieldMap = new Dictionary<string, int>();
    private readonly IReadOnlyDictionary<string, int> _fieldIndicesByName;

    public static readonly TupleValue Empty = new(Array.Empty<Value>(), EmptyFieldMap);

    public TupleValue(IReadOnlyList<Value> items, IReadOnlyDictionary<string, int> fieldIndicesByName)
    {
        _fieldIndicesByName = fieldIndicesByName;

        var maxIndex = fieldIndicesByName.Count > 0
            ? fieldIndicesByName.Max(kvp => kvp.Value) + 1
            : 0;
        var fieldNamesByIndex = new string[maxIndex];
        foreach (var (key, value) in fieldIndicesByName)
        {
            fieldNamesByIndex[value] = key;
        }

        FieldNames = fieldNamesByIndex;
        Items = items;
    }

    public IReadOnlyList<Value> Items { get; }

    public IReadOnlyList<string> FieldNames { get; }

    public Value this[string fieldName] => Items[_fieldIndicesByName[fieldName]];

    public static TupleValue Of(params Value[] items) => new(items, EmptyFieldMap);

    public static TupleValue Of(params (string name, Value value)[] fields)
    {
        var values = new Value[fields.Length];
        var fieldIndicesByName = new Dictionary<string, int>();
        for (var i = 0; i < fields.Length; i++)
        {
            values[i] = fields[i].value;
            fieldIndicesByName[fields[i].name] = i;
        }

        return new TupleValue(values, fieldIndicesByName);
    }

    public static TupleValue Combine(TupleValue left, TupleValue right)
    {
        var newValues = new List<Value>(left.Items);
        var newFieldIndicesByName = new Dictionary<string, int>(left._fieldIndicesByName);

        for (var i = 0; i < right.Items.Count; i++)
        {
            var rightFieldName = i < right.FieldNames.Count ? right.FieldNames[i] : null;
            if (string.IsNullOrEmpty(rightFieldName))
            {
                newValues.Add(right.Items[i]);
                continue;
            }
            if (left._fieldIndicesByName.TryGetValue(rightFieldName, out var leftIndex))
            {
                newValues[leftIndex] = right.Items[i];
            }
            else
            {
                newValues.Add(right.Items[i]);
                newFieldIndicesByName[rightFieldName] = newValues.Count - 1;
            }
        }

        return new TupleValue(newValues, newFieldIndicesByName);
    }

    public override string ToString() =>
        $"{GetType().Name}[{string.Join(", ", Items.Select(v => v.ToString()))}]";

    public override string AsString() =>
        '(' + string.Join(", ", Items.Select(FormatField)) + ')';

    private string FormatField(Value value, int index) =>
        index < 0 || index >= FieldNames.Count || string.IsNullOrEmpty(FieldNames[index])
            ? value.AsString()
            : $"{FieldNames[index]}: {value.AsString()}";
    
    public override bool AsBoolean() => true;

    public bool Equals(TupleValue? other) =>
        ReferenceEquals(null, other) == false
        && ValueCollections.Equal(Items, other.Items);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TupleValue)obj);
    }

    public override int GetHashCode() =>
        ValueCollections.GetHashCode(Items);
}

public interface IRedisKey
{
    RedisKey AsRedisKey();
}

public interface IRedisValue
{
    SR.RedisValue AsRedisValue();
}

public interface IRealValue
{
    double AsRealValue();
}

public class StringValue : ScalarValue<string>, IRedisKey, IRedisValue
{
    public static readonly StringValue Empty = new(string.Empty);

    public StringValue(string value) : base(value)
    {}

    public StringValue Slice(int lower, int upper) => new(Value[lower .. (upper + 1)]);

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
    public RedisValue(SR.RedisValue value) : base(value)
    {}

    public static readonly RedisValue Empty = new(SR.RedisValue.Null);

    public override string AsString() => Value.HasValue ? Value : string.Empty;
    public override bool AsBoolean() => Value.HasValue;
    public RedisKey AsRedisKey() => new(Value);
    public SR.RedisValue AsRedisValue() => Value;
}

public class IntegerValue : ScalarValue<long>, IRedisValue, IRealValue
{
    private static readonly IntegerValue[] CachedValues = Enumerable.Range(0, 100)
        .Select(n => new IntegerValue(n))
        .ToArray();

    private IntegerValue(long value) : base(value)
    {}

    public static IntegerValue Zero => CachedValues[0];

    public static IntegerValue Of(long n) =>
        n >= 0 && n < CachedValues.Length ? CachedValues[n] : new IntegerValue(n);

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value != 0;
    public SR.RedisValue AsRedisValue() => Value;
    public double AsRealValue() => Value;
}

public class RealValue : ScalarValue<double>, IRedisValue, IRealValue
{
    public static readonly RealValue Zero = new(0);

    public RealValue(double value) : base(value)
    {}

    public override string AsString() => Value.ToString(CultureInfo.InvariantCulture);
    public override bool AsBoolean() => Value != 0.0;
    public SR.RedisValue AsRedisValue() => Value;
    public double AsRealValue() => Value;
}

public class BoolValue : ScalarValue<bool>, IRedisValue
{
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    private BoolValue(bool value) : base(value)
    {}

    public static BoolValue Of(bool value) => value ? True : False;

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value;
    public SR.RedisValue AsRedisValue() => Value;
}

public class RangeValue : EnumerableValue
{
    public RangeValue(long lower, long upper) : base(WalkRange(lower, upper))
    {
        Lower = lower;
        Upper = upper;
    }

    public long Lower { get; }
    public long Upper { get; }

    public override bool AsBoolean() => Upper >= Lower;
    public override string ToString() => $"{GetType().Name}[{Lower} .. {Upper}]";

    private static async IAsyncEnumerable<Value> WalkRange(long lower, long upper)
    {
        for (; lower <= upper; lower++)
        {
            yield return await Task.FromResult(IntegerValue.Of(lower)).ConfigureAwait(false);
        }
    }
}

public class TimestampValue : ScalarValue<DateTimeOffset>, IRedisValue
{
    public const string StandardFormatString = "yyyy-MM-dd HH:mm:ss zz";

    public TimestampValue(DateTimeOffset value) : base(value)
    {}

    public override string AsString() =>
        Value.ToString(StandardFormatString);

    public override bool AsBoolean() => Value.ToUnixTimeSeconds() != 0;
    public SR.RedisValue AsRedisValue() => AsString();
}

public class DurationValue : ScalarValue<TimeSpan>, IRedisValue
{
    public const string StandardFormatString = "g";// "general short" = d.hh.mm.ss.ttt

    public DurationValue(TimeSpan value) : base(value)
    {}

    public override string AsString() => Value.ToString(StandardFormatString);

    public override bool AsBoolean() => Value != TimeSpan.Zero;
    public SR.RedisValue AsRedisValue() => AsString();
}

public class FunctionValue : Value
{
    public FunctionValue(string name, IReadOnlyList<string> parameters, Expr body)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
    }

    public string Name { get; }
    public IReadOnlyList<string> Parameters { get; }
    public Expr Body { get; }

    public override string AsString() =>
        $"{Name}({string.Join(", ", Parameters)})";

    public override bool AsBoolean() => true;
}