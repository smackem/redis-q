using System.Text;

namespace RedisQ.Core.Runtime;

public partial class FunctionRegistry
{
    private void RegisterCommonFunctions()
    {
        Register(new FunctionDefinition("size", 1, FuncSize));
        Register(new FunctionDefinition("count", 1, FuncCount));
        Register(new FunctionDefinition("int", 1, FuncInt));
        Register(new FunctionDefinition("real", 1, FuncReal));
        Register(new FunctionDefinition("bool", 1, FuncBool));
        Register(new FunctionDefinition("string", 1, FuncString));
        Register(new FunctionDefinition("lower", 1, FuncLower));
        Register(new FunctionDefinition("upper", 1, FuncUpper));
        Register(new FunctionDefinition("collect", 1, FuncCollect));
        Register(new FunctionDefinition("join", 2, FuncJoin));
        Register(new FunctionDefinition("distinct", 1, FuncDistinct));
        Register(new FunctionDefinition("sum", 1, FuncSum));
        Register(new FunctionDefinition("avg", 1, FuncAvg));
        Register(new FunctionDefinition("min", 1, FuncMin));
        Register(new FunctionDefinition("max", 1, FuncMax));
        Register(new FunctionDefinition("reverse", 1, FuncReverse));
    }

    private static Task<Value> FuncSize(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => Task.FromResult<Value>(IntegerValue.Of(list.Count)),
            StringValue str => Task.FromResult<Value>(IntegerValue.Of(str.Value.Length)),
            IRedisValue val => Task.FromResult<Value>(IntegerValue.Of(val.AsRedisValue().Length())),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static async Task<Value> FuncCount(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => IntegerValue.Of(list.Count),
            EnumerableValue enumerable => IntegerValue.Of(await CountEnumerable(enumerable).ConfigureAwait(false)),
            _ => NullValue.Instance,
        };

    private static async Task<int> CountEnumerable(EnumerableValue enumerable)
    {
        var count = 0;
        await foreach (var _ in enumerable.ConfigureAwait(false)) count++;
        return count;
    }
    
    private static Task<Value> FuncInt(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            IntegerValue n => Task.FromResult<Value>(n),
            RealValue real => Task.FromResult<Value>(IntegerValue.Of((long) real.Value)),
            CharValue ch => Task.FromResult<Value>(IntegerValue.Of(ch.Value)),
            StringValue str => Task.FromResult<Value>(
                int.TryParse(str.Value, out var n) ? IntegerValue.Of(n) : NullValue.Instance),
            IRedisValue val => Task.FromResult<Value>(
                val.AsRedisValue().TryParse(out int n) ? IntegerValue.Of(n) : NullValue.Instance),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static Task<Value> FuncReal(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            IntegerValue n => Task.FromResult<Value>(new RealValue(n.Value)),
            RealValue real => Task.FromResult<Value>(real),
            CharValue ch => Task.FromResult<Value>(new RealValue(ch.Value)),
            StringValue str => Task.FromResult<Value>(
                double.TryParse(str.Value, out var d) ? new RealValue(d) : NullValue.Instance),
            IRedisValue val => Task.FromResult<Value>(
                val.AsRedisValue().TryParse(out double d) ? new RealValue(d) : NullValue.Instance),
            _ => Task.FromResult<Value>(NullValue.Instance),
        };

    private static Task<Value> FuncBool(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            EnumerableValue or TupleValue => Task.FromResult<Value>(NullValue.Instance),
            var v => Task.FromResult<Value>(BoolValue.Of(v.AsBoolean())),
        };

    private static Task<Value> FuncString(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            EnumerableValue => Task.FromResult<Value>(NullValue.Instance),
            var v => Task.FromResult<Value>(new StringValue(v.AsString())),
        };

    private static Task<Value> FuncLower(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(arguments[0].AsString().ToLower()));

    private static Task<Value> FuncUpper(Context ctx, Value[] arguments) =>
        Task.FromResult<Value>(new StringValue(arguments[0].AsString().ToUpper()));

    private static async Task<Value> FuncCollect(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            EnumerableValue e => new ListValue(await e.Collect(1000).ConfigureAwait(false)),
            _ => throw new RuntimeException($"collect({arguments[0]}): incompatible operand, enumerable expected"), 
        };

    private static async Task<Value> FuncJoin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is StringValue separator == false) throw new RuntimeException($"join({arguments[0]}): incompatible operand, string expected");
        if (arguments[1] is EnumerableValue coll == false) throw new RuntimeException($"join({arguments[1]}): incompatible operand, enumerable expected");

        var sb = new StringBuilder();
        var count = 0;
        
        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (count >= 1000) break;
            if (count != 0) sb.Append(separator.Value);
            sb.Append(value.AsString());
            count++;
        }

        return new StringValue(sb.ToString());
    }

    private static Task<Value> FuncDistinct(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"distinct({arguments[0]}): incompatible operand, enumerable expected");
        var set = new HashSet<Value>();

        async IAsyncEnumerable<Value> Walk()
        {
            await foreach (var value in coll.ConfigureAwait(false))
            {
                if (set.Add(value)) yield return value;
            }
        }

        return Task.FromResult<Value>(new EnumerableValue(Walk()));
    }

    private static async Task<Value> FuncSum(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"sum({arguments[0]}): incompatible operand, enumerable expected");
        Value? sum = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (value is NullValue) continue;

            sum = sum != null ? ValueOperations.Add(sum, value) : value;
        }

        return sum ?? NullValue.Instance;
    }

    private static async Task<Value> FuncAvg(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        var sum = 0.0;
        var count = 0;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (value is IRealValue real == false) continue;

            sum += real.AsRealValue();
            count++;
        }

        return count > 0 ? new RealValue(sum / count) : NullValue.Instance;
    }

    private static async Task<Value> FuncMin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        Value? min = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (min == null)
            {
                min = value;
                continue;
            }
            if (ValueComparer.Default.Compare(value, min) < 0) min = value;
        }

        return min ?? NullValue.Instance;
    }

    private static async Task<Value> FuncMax(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"avg({arguments[0]}): incompatible operand, enumerable expected");
        Value? max = null;

        await foreach (var value in coll.ConfigureAwait(false))
        {
            if (max == null)
            {
                max = value;
                continue;
            }
            if (ValueComparer.Default.Compare(value, max) > 0) max = value;
        }

        return max ?? NullValue.Instance;
    }

    private static async Task<Value> FuncReverse(Context ctx, Value[] arguments)
    {
        if (arguments[0] is EnumerableValue coll == false) throw new RuntimeException($"reverse({arguments[0]}): incompatible operand, enumerable expected");
        var stack = new Stack<Value>();

        await foreach (var value in coll.ConfigureAwait(false))
        {
            stack.Push(value);
        }

        return coll is ListValue
            ? new ListValue(stack.ToArray())
            : new EnumerableValue(AsyncEnumerable.FromCollection(stack));
    }
}
