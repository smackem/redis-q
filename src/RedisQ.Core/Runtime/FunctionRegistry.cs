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
        if (arguments[0] is IRedisValue pattern == false) throw new RuntimeException("");
        var keys = ctx.Redis.ScanKeys(pattern.AsRedisValue());

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var key in keys)
            {
                yield return new RedisKeyValue(key);
            }
        }

        return Task.FromResult<Value>(new EnumerableValue(Scan()));
    }
    
    // len(enumerable|string)
    // get(key) -> redisValue
    // hkeys(key) -> enumerable<key>
    //

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