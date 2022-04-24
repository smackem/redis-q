using System.Text;
using StackExchange.Redis;

namespace RedisQ.Core.Runtime;

public class FunctionRegistry
{
    private readonly IDictionary<string, FunctionDefinition> _dict = new Dictionary<string, FunctionDefinition>();

    public FunctionRegistry()
    {
        // redis functions
        Register(new FunctionDefinition("keys", 1, FuncKeys));
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
        Register(new FunctionDefinition("type", 1, FuncType));
        Register(new FunctionDefinition("hstrlen", 2, FuncHStrLen));
        Register(new FunctionDefinition("smembers", 1, FuncSMembers));
        Register(new FunctionDefinition("scard", 1, FuncSCard));
        Register(new FunctionDefinition("sdiff", 2, FuncSDiff));
        Register(new FunctionDefinition("sinter", 2, FuncSInter));
        Register(new FunctionDefinition("sunion", 2, FuncSUnion));
        Register(new FunctionDefinition("sismember", 2, FuncSIsMember));

        // zscan
        // zcard
        // zcount
        // zdiff
        // zinter
        // zintercard
        // zrange
        // zrangebyscore
        // zrangebylex
        // zrank
        // zscore
        // zunion

        // util functions
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
    }

    private static async Task<Value> FuncSMembers(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"smembers({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var members = await db.SetMembersAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new ListValue(members.Select(m => new RedisValue(m)).ToArray());
    }

    private static async Task<Value> FuncSCard(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"scard({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var card = await db.SetLengthAsync(key.AsRedisKey()).ConfigureAwait(false);
        return IntegerValue.Of((int)card);
    }

    private static Task<Value> FuncSDiff(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Difference);

    private static Task<Value> FuncSInter(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Intersect);

    private static Task<Value> FuncSUnion(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Union);

    private static async Task<Value> CombineSets(Context ctx, Value[] arguments, SetOperation operation)
    {
        if (arguments[0] is IRedisKey key1 == false) throw new RuntimeException($"sdiff({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisKey key2 == false) throw new RuntimeException($"sdiff({arguments[1]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var resultSet = await db.SetCombineAsync(operation, key1.AsRedisKey(), key2.AsRedisKey());
        return new ListValue(resultSet.Select(m => new RedisValue(m)).ToArray());
    }

    private static async Task<Value> FuncSIsMember(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"sismember({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"sismember({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var found = await db.SetContainsAsync(key.AsRedisKey(), value.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(found);
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

    private static async Task<Value> FuncHStrLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hstrlen({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hstrlen({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.HashStringLengthAsync(key.AsRedisKey(), field.AsRedisValue());
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
            .Select(entry => TupleValue.Of(new RedisValue(entry.Name), new RedisValue(entry.Value)))
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

    private static async Task<Value> FuncType(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"type({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.KeyTypeAsync(key.AsRedisKey());
        return new StringValue(val.ToString().ToLower());
    }

    private static Task<Value> FuncSize(Context ctx, Value[] arguments) =>
        arguments[0] switch
        {
            ListValue list => Task.FromResult<Value>(IntegerValue.Of(list.Count)),
            StringValue str => Task.FromResult<Value>(IntegerValue.Of(str.Value.Length)),
            IRedisValue val => Task.FromResult<Value>(IntegerValue.Of((int) val.AsRedisValue().Length())),
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
            RealValue real => Task.FromResult<Value>(IntegerValue.Of((int) real.Value)),
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

    public FunctionDefinition Resolve(string name, int parameterCount)
    {
        if (!_dict.TryGetValue(name, out var function)) throw new Exception($"function {name} not found");
        if (function.ParameterCount != parameterCount) throw new Exception($"parameter count mismatch for function {name}: expected {function.ParameterCount}, got {parameterCount}");

        return function;
    }

    private void Register(FunctionDefinition function) =>
        _dict.Add(function.Name, function);
}