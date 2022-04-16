namespace RedisQ.Core.Runtime;

public class FunctionRegistry
{
    private readonly IDictionary<string, FunctionDefinition> _dict = new Dictionary<string, FunctionDefinition>();

    public FunctionRegistry()
    {
        Register(new FunctionDefinition("keys", 1, FuncKeys));
        Register(new FunctionDefinition("len", 1, FuncLen));
        Register(new FunctionDefinition("get", 1, FuncGet));
        Register(new FunctionDefinition("mget", 1, FuncMGet));
    }

    private static Task<Value> FuncKeys(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisValue pattern == false) throw new RuntimeException($"keys({arguments[0]}): incompatible operand, RedisValue expected");
        var keys = ctx.Redis.ScanKeys(pattern.AsRedisValue());

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var key in keys) yield return new RedisKeyValue(key);
        }

        return Task.FromResult<Value>(new EnumerableValue(Scan()));
    }

    private static Task<Value> FuncLen(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => Task.FromResult(IntegerValue.Of(list.Count) as Value),
            StringValue str => Task.FromResult(IntegerValue.Of(str.Value.Length) as Value),
            IRedisValue val => Task.FromResult(IntegerValue.Of((int) val.AsRedisValue().Length()) as Value),
            _ => throw new RuntimeException($"len({arguments[0]}): incompatible operand, List or String or RedisValue expected"),
        };

    private static async Task<Value> FuncGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"get({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.StringGetAsync(key.AsRedisKey());
        return new RedisValue(val);
    }

    private static async Task<Value> FuncMGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is ListValue list == false) throw new RuntimeException($"get({arguments[0]}): incompatible operand, List expected");
        var keys = list.Select(v => v switch
        {
            IRedisKey k => k.AsRedisKey(),
            _ => throw new RuntimeException($"get(): unexpected value in argument list: {v}. RedisKey expected."),
        }).ToArray();
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.StringGetAsync(keys).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    // strlen(key) -> int
    // getrange(key, int, int) -> redisValue
    // hkeys(key) -> enumerable<key>
    // hget(key, redisValue) -> redisValue
    // hgetall(key) -> enumerable<tuple(redisValue, redisValue)>
    // llen(key) -> int
    // lrange(key, int, int) -> enumerable<redisValue>
    // lindex(key, int) -> redisValue

    public FunctionDefinition Resolve(string name, int parameterCount)
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