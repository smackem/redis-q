namespace RedisQ.Core.Runtime;

public abstract class Value
{
    public abstract string AsString();
    public abstract bool AsBoolean();
}

public abstract class ScalarValue<T> : Value
{
    public ScalarValue(T value)
    {
        Value = value;
    }

    public T Value { get; }
}

public class VectorValue : Value, IAsyncEnumerable<Value>
{
    private readonly IAsyncEnumerable<Value> _enumerable;

    public VectorValue(IAsyncEnumerable<Value> enumerable)
    {
        _enumerable = enumerable;
    }
    
    public IAsyncEnumerator<Value> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return _enumerable.GetAsyncEnumerator(cancellationToken);
    }

    public override string AsString() => throw new NotSupportedException();
    public override bool AsBoolean() => throw new NotSupportedException();
}

public class KeyValue : ScalarValue<string>
{
    public KeyValue(string value) : base(value)
    {
    }

    public override string AsString() => Value;
    public override bool AsBoolean() => string.IsNullOrEmpty(Value) == false;
}

public class StringValue : ScalarValue<string>
{
    public StringValue(string value) : base(value)
    {
    }

    public override string AsString() => Value;
    public override bool AsBoolean() => string.IsNullOrEmpty(Value) == false;
}

public class IntegerValue : ScalarValue<int>
{
    public IntegerValue(int value) : base(value)
    {
    }

    public override string AsString() => Value.ToString();
    public override bool AsBoolean() => Value != 0;
}
