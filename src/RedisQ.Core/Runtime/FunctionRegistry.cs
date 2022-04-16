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
        Register(new FunctionDefinition("strlen", 1, FuncStrLen));
        Register(new FunctionDefinition("getrange", 3, FuncGetRange));
        Register(new FunctionDefinition("hkeys", 1, FuncHKeys));
        Register(new FunctionDefinition("hget", 2, FuncHGet));
        Register(new FunctionDefinition("hgetall", 1, FuncHGetAll));
        Register(new FunctionDefinition("llen", 1, FuncLLen));
        Register(new FunctionDefinition("lrange", 3, FuncLRange));
        Register(new FunctionDefinition("lindex", 2, FuncLIndex));
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
        if (arguments[0] is ListValue list == false) throw new RuntimeException($"mget({arguments[0]}): incompatible operand, List expected");
        var keys = list.Select(v => v switch
        {
            IRedisKey k => k.AsRedisKey(),
            _ => throw new RuntimeException($"mget(): unexpected value in argument list: {v}. RedisKey expected."),
        }).ToArray();
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.StringGetAsync(keys).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncStrLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"strlen({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.StringLengthAsync(key.AsRedisKey());
        return IntegerValue.Of((int) val);
    }

    private static async Task<Value> FuncGetRange(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"getrange({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue start == false) throw new RuntimeException($"getrange({arguments[1]}): incompatible operand, Integer expected");
        if (arguments[2] is IntegerValue end == false) throw new RuntimeException($"getrange({arguments[2]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.StringGetRangeAsync(key.AsRedisKey(), start.Value, end.Value);
        return new RedisValue(val);
    }
    
    private static async Task<Value> FuncHKeys(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hkeys({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.HashKeysAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncHGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hget({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hget({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.HashGetAsync(key.AsRedisKey(), field.AsRedisValue());
        return new RedisValue(val);
    }

    private static async Task<Value> FuncHGetAll(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hgetall({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var entries = await db.HashGetAllAsync(key.AsRedisKey());
        var tuples = entries
            .Select(entry => TupleValue.Create(new RedisValue(entry.Name), new RedisValue(entry.Value)))
            .Cast<Value>()
            .ToArray();
        return new ListValue(tuples);
    }

    private static async Task<Value> FuncLLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"llen({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var length = await db.ListLengthAsync(key.AsRedisKey());
        return IntegerValue.Of((int) length);
    }

    private static async Task<Value> FuncLRange(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"llen({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue start == false) throw new RuntimeException($"llen({arguments[1]}): incompatible operand, Integer expected");
        if (arguments[2] is IntegerValue stop == false) throw new RuntimeException($"llen({arguments[2]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.ListRangeAsync(key.AsRedisKey(), start.Value, stop.Value);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncLIndex(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"lindex({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue index == false) throw new RuntimeException($"lindex({arguments[1]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.ListGetByIndexAsync(key.AsRedisKey(), index.Value);
        return new RedisValue(val);
    }

    public FunctionDefinition Resolve(string name, int parameterCount)
    {
        if (!_dict.TryGetValue(name, out var function)) throw new Exception($"function {name} not found");
        if (function.ParameterCount != parameterCount) throw new Exception($"parameter count mismatch for function {name}: expected {function.ParameterCount}, got {parameterCount}");

        return function;
    }

    public void Register(FunctionDefinition function)
    {
        _dict.Add(function.Name, function);
    }
}