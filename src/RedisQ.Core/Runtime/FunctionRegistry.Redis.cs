using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace RedisQ.Core.Runtime;

// almost all redis function names produce typo/naming warnings 
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public partial class FunctionRegistry
{
    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    private void RegisterRedisFunctions()
    {
        Register(new("KEYS", 1, FuncKeys, "(pattern) -> enumerable"));
        Register(new("SCAN", 1, FuncKeys, "(pattern) -> enumerable"));
        Register(new("GET", 1, FuncGet, "(key) -> value"));
        Register(new("MGET", 1, FuncMGet, "(list of keys) -> list of values"));
        Register(new("STRLEN", 1, FuncStrLen, "(key) -> int"));
        Register(new("GETRANGE", 3, FuncGetRange, "(key, start: int, end: int) -> value"));
        Register(new("TYPE", 1, FuncType, "(key) -> string"));
        Register(new("EXISTS", 1, FuncExists, "(key) -> bool"));
        Register(new("RANDOMKEY", 0, FuncRandomKey, "() -> key"));
        Register(new("HKEYS", 1, FuncHKeys, "(key) -> list of keys"));
        Register(new("HGET", 2, FuncHGet, "(key, field: value) -> value"));
        Register(new("HGETALL", 1, FuncHGetAll, "(key) -> list of tuple(name: string, value: value)"));
        Register(new("HSTRLEN", 2, FuncHStrLen, "(key, field: value) -> int"));
        Register(new("HEXISTS", 2, FuncHExists, "(key, field: value) -> bool"));
        Register(new("HLEN", 1, FuncHLen, "(key) -> int"));
        Register(new("HMGET", 2, FuncHMGet, "(key, list of field: value) -> list of value"));
        Register(new("HSCAN", 2, FuncHScan, "(key, pattern: value) -> enumerable"));
        Register(new("HVALS", 1, FuncHVals, "(key) -> list of value"));
        Register(new("LLEN", 1, FuncLLen, "(key) -> int"));
        Register(new("LRANGE", 3, FuncLRange, "(key, start: int, end: int) -> list"));
        Register(new("LINDEX", 2, FuncLIndex, "(key, index: int) -> value"));
        Register(new("SMEMBERS", 1, FuncSMembers, "(key) -> list of value"));
        Register(new("SCARD", 1, FuncSCard, "(key) -> int"));
        Register(new("SDIFF", 2, FuncSDiff, "(key, key) -> list of value"));
        Register(new("SINTER", 2, FuncSInter, "(key, key) -> list of value"));
        Register(new("SUNION", 2, FuncSUnion, "(key, key) -> list of value"));
        Register(new("SISMEMBER", 2, FuncSIsMember, "(key, value) -> bool"));
        Register(new("SSCAN", 2, FuncSScan, "(key, pattern: value) -> enumerable"));
        Register(new("SRANDOMMEMBER", 1, FuncSRandomMember, "(key) -> value"));
        Register(new("ZCARD", 1, FuncZCard, "(key) -> int"));
        Register(new("ZCOUNT", 3, FuncZCount, "(key, minScore: real, maxScore: real) -> int"));
        Register(new("ZRANGE", 3, FuncZRange, "(key, start: int, end: int) -> list of value"));
        Register(new("ZRANGEBYSCORE", 3, FuncZRangeByScore, "(key, minScore: real, maxScore: real) -> list of value"));
        Register(new("ZRANK", 2, FuncZRank, "(key, value) -> int"));
        Register(new("ZSCORE", 2, FuncZScore, "(key, value) -> real"));
        Register(new("ZSCAN", 2, FuncZScan, "(key, pattern: value) -> enumerable"));
    }

    private static async Task<Value> FuncExists(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"exists({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.KeyExistsAsync(key.AsRedisKey()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncRandomKey(Context ctx, Value[] arguments)
    {
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var key = await db.KeyRandomAsync().ConfigureAwait(false);
        return new RedisKeyValue(key);
    }

    private static async Task<Value> FuncZScan(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zscan({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is StringValue pattern == false) throw new RuntimeException($"zscan({arguments[1]}): incompatible operand, string expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);

        async IAsyncEnumerable<Value> Walk(IAsyncEnumerable<SortedSetEntry> entries)
        {
            await foreach (var entry in entries.ConfigureAwait(false))
            {
                yield return TupleValue.Of(("member", new RedisValue(entry.Element)), ("score", new RealValue(entry.Score)));
            }
        }

        var entries = db.SortedSetScanAsync(key.AsRedisKey(), pattern.Value);
        return new EnumerableValue(Walk(entries));
    }

    private static async Task<Value> FuncZRank(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zrank({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"zrank({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var rank = await db.SortedSetRankAsync(key.AsRedisKey(), value.AsRedisValue()).ConfigureAwait(false);
        return rank != null ? IntegerValue.Of(rank.Value) : NullValue.Instance;
    }

    private static async Task<Value> FuncZScore(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zscore({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"zscore({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var score = await db.SortedSetScoreAsync(key.AsRedisKey(), value.AsRedisValue()).ConfigureAwait(false);
        return score != null ? new RealValue(score.Value) : NullValue.Instance;
    }

    private static async Task<Value> FuncZRangeByScore(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zrangebyscore({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRealValue min == false) throw new RuntimeException($"zrangebyscore({arguments[1]}): incompatible operand, real expected");
        if (arguments[2] is IRealValue max == false) throw new RuntimeException($"zrangebyscore({arguments[2]}): incompatible operand, real expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var range = await db.SortedSetRangeByScoreAsync(key.AsRedisKey(), min.AsRealValue(), max.AsRealValue()).ConfigureAwait(false);
        return new ListValue(range.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncZRange(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zrange({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue start == false) throw new RuntimeException($"zrange({arguments[1]}): incompatible operand, integer expected");
        if (arguments[2] is IntegerValue end == false) throw new RuntimeException($"zrange({arguments[2]}): incompatible operand, integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var range = await db.SortedSetRangeByRankAsync(key.AsRedisKey(), start.Value, end.Value).ConfigureAwait(false);
        return new ListValue(range.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncZCount(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zcount({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue min == false) throw new RuntimeException($"zcount({arguments[1]}): incompatible operand, RedisValue expected");
        if (arguments[2] is IRedisValue max == false) throw new RuntimeException($"zcount({arguments[2]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var count = await db.SortedSetLengthByValueAsync(key.AsRedisKey(), min.AsRedisValue(), max.AsRedisValue()).ConfigureAwait(false);
        return IntegerValue.Of(count);
    }

    private static async Task<Value> FuncZCard(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zcard({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var card = await db.SortedSetLengthAsync(key.AsRedisKey()).ConfigureAwait(false);
        return IntegerValue.Of(card);
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
        return IntegerValue.Of(card);
    }

    private static Task<Value> FuncSDiff(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Difference);

    private static Task<Value> FuncSInter(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Intersect);

    private static Task<Value> FuncSUnion(Context ctx, Value[] arguments) =>
        CombineSets(ctx, arguments, SetOperation.Union);

    private static async Task<Value> CombineSets(Context ctx, Value[] arguments, SetOperation operation)
    {
        if (arguments[0] is IRedisKey key1 == false) throw new RuntimeException($"set_combine({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisKey key2 == false) throw new RuntimeException($"set_combine({arguments[1]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var resultSet = await db.SetCombineAsync(operation, key1.AsRedisKey(), key2.AsRedisKey()).ConfigureAwait(false);
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

    private static async Task<Value> FuncSRandomMember(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"srandommember({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var value = await db.SetRandomMemberAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new RedisValue(value);
    }

    private static Task<Value> FuncKeys(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisValue pattern == false) throw new RuntimeException($"keys({arguments[0]}): incompatible operand, RedisValue expected");
        var keys = ctx.Redis.ScanKeys(pattern.AsRedisValue());

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var key in keys.ConfigureAwait(false)) yield return new RedisKeyValue(key);
        }

        return Task.FromResult<Value>(new EnumerableValue(Scan()));
    }

    private static async Task<Value> FuncGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"get({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.StringGetAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new RedisValue(val);
    }

    // ReSharper disable once InconsistentNaming
    private static async Task<Value> FuncHMGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hmget({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is ListValue list == false) throw new RuntimeException($"hmget({arguments[1]}): incompatible operand, List expected");
        var fields = list.Select(v => v switch
        {
            IRedisValue k => k.AsRedisValue(),
            _ => throw new RuntimeException($"hmget(): unexpected value in argument list: {v}. RedisValue expected."),
        }).ToArray();
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.HashGetAsync(key.AsRedisKey(), fields).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
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
        var val = await db.StringLengthAsync(key.AsRedisKey()).ConfigureAwait(false);
        return IntegerValue.Of(val);
    }

    private static async Task<Value> FuncHStrLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hstrlen({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hstrlen({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.HashStringLengthAsync(key.AsRedisKey(), field.AsRedisValue()).ConfigureAwait(false);
        return IntegerValue.Of(val);
    }

    private static async Task<Value> FuncGetRange(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"getrange({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue start == false) throw new RuntimeException($"getrange({arguments[1]}): incompatible operand, Integer expected");
        if (arguments[2] is IntegerValue end == false) throw new RuntimeException($"getrange({arguments[2]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.StringGetRangeAsync(key.AsRedisKey(), start.Value, end.Value).ConfigureAwait(false);
        return new RedisValue(val);
    }

    private static async Task<Value> FuncHKeys(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hkeys({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.HashKeysAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    // ReSharper disable once IdentifierTypo
    private static async Task<Value> FuncHVals(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hkeys({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.HashValuesAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncHGet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hget({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hget({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.HashGetAsync(key.AsRedisKey(), field.AsRedisValue()).ConfigureAwait(false);
        return new RedisValue(val);
    }

    private static async Task<Value> FuncHExists(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hexists({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hexists({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.HashExistsAsync(key.AsRedisKey(), field.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncHGetAll(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hgetall({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var entries = await db.HashGetAllAsync(key.AsRedisKey()).ConfigureAwait(false);
        var tuples = entries
            .Select(entry => TupleValue.Of(("name", new RedisValue(entry.Name)), ("value", new RedisValue(entry.Value))))
            .Cast<Value>()
            .ToArray();
        return new ListValue(tuples);
    }

    private static async Task<Value> FuncHScan(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hscan({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue pattern == false) throw new RuntimeException($"hscan({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var entries = db.HashScanAsync(key.AsRedisKey(), pattern.AsRedisValue());

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var entry in entries.ConfigureAwait(false))
            {
                yield return TupleValue.Of(
                    ("name", new StringValue(entry.Name)),
                    ("value", new RedisValue(entry.Value)));
            }
        }

        return new EnumerableValue(Scan());
    }

    private static async Task<Value> FuncSScan(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"sscan({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue pattern == false) throw new RuntimeException($"sscan({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = db.SetScanAsync(key.AsRedisKey(), pattern.AsRedisValue());

        async IAsyncEnumerable<Value> Scan()
        {
            await foreach (var v in values.ConfigureAwait(false)) yield return new RedisValue(v);
        }

        return new EnumerableValue(Scan());
    }

    private static async Task<Value> FuncHLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hlen({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var length = await db.HashLengthAsync(key.AsRedisKey()).ConfigureAwait(false);
        return IntegerValue.Of(length);
    }

    private static async Task<Value> FuncLLen(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"llen({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var length = await db.ListLengthAsync(key.AsRedisKey()).ConfigureAwait(false);
        return IntegerValue.Of(length);
    }

    private static async Task<Value> FuncLRange(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"llen({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue start == false) throw new RuntimeException($"llen({arguments[1]}): incompatible operand, Integer expected");
        if (arguments[2] is IntegerValue stop == false) throw new RuntimeException($"llen({arguments[2]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var values = await db.ListRangeAsync(key.AsRedisKey(), start.Value, stop.Value).ConfigureAwait(false);
        return new ListValue(values.Select(v => new RedisValue(v)).ToArray());
    }

    private static async Task<Value> FuncLIndex(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"lindex({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue index == false) throw new RuntimeException($"lindex({arguments[1]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.ListGetByIndexAsync(key.AsRedisKey(), index.Value).ConfigureAwait(false);
        return new RedisValue(val);
    }

    private static async Task<Value> FuncType(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"type({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var val = await db.KeyTypeAsync(key.AsRedisKey()).ConfigureAwait(false);
        return new StringValue(val.ToString().ToLower());
    }
}