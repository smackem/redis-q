using System.Collections;

namespace RedisQ.Core.Runtime;

public class FunctionRegistry
{
    private readonly IDictionary<string, FunctionDefinition> _dict = new Dictionary<string, FunctionDefinition>();

    public FunctionRegistry()
    {
        Register(new FunctionDefinition("keys", 1, FuncKeys));
    }

    private static Task<Value> FuncKeys(Context ctx, Value[] arguments)
    {
        var pattern = arguments[0].AsString();
        var keys = ctx.Redis.ScanKeys(pattern);

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var key in keys)
            {
                yield return new KeyValue(key);
            }
        }

        return Task.FromResult<Value>(new VectorValue(Scan()));
    }

    public FunctionDefinition Lookup(string name, int parameterCount)
    {
        if (!_dict.TryGetValue(name, out var function))
        {
            throw new Exception($"function {name} not found");
        }
        if (function.ParameterCount != parameterCount)
        {
            throw new Exception($"parameter count mismatch for function {name}: expected {function.ParameterCount}, got {parameterCount}");
        }

        return function;
    }

    private void Register(FunctionDefinition function)
    {
        _dict.Add(function.Name, function);
    }
}