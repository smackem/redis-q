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
        Register(new("BITPOS", 4, FuncBitPos, "(key, bit: bool, start: int, end: int) -> int"));
        Register(new("BITCOUNT", 3, FuncBitCount, "(key, start: int, end: int) -> int"));
        Register(new("GETBIT", 2, FuncGetBit, "(key, offset: int) -> bool"));
        Register(new("TTL", 1, FuncTtl, "(key) -> duration"));
        // mutating functions
        Register(new("DEL", 1, FuncDel, "(key or list of keys) -> int"));
        Register(new("EXPIRE", 2, FuncExpire, "(key, duration) -> bool"));
        Register(new("RENAME", 2, FuncRename, "(key, new: key) -> bool"));
        Register(new("SET", 2, FuncSet, "(key, value) -> bool"));
        Register(new("HSET", 3, FuncHSet, "(key, field: value, value) -> bool"));
        Register(new("HDEL", 2, FuncHDel, "(key, field: value) -> bool"));
        Register(new("LPOP", 1, FuncLPop, "(key) -> value"));
        Register(new("RPOP", 1, FuncRPop, "(key) -> value"));
        Register(new("LPUSH", 2, FuncLPush, "(key, value) -> int"));
        Register(new("RPUSH", 2, FuncRPush, "(key, value) -> int"));
        Register(new("LSET", 3, FuncLSet, "(key, index, value) -> null"));
        Register(new("SADD", 2, FuncSAdd, "(key, value) -> bool"));
        Register(new("SREM", 2, FuncSRem, "(key, value) -> bool"));
        Register(new("ZADD", 3, FuncZAdd, "(key, value, score) -> bool"));
        Register(new("ZPOPMIN", 1, FuncZPopMin, "(key) -> tuple of (value, score: real)"));
        Register(new("ZPOPMAX", 1, FuncZPopMax, "(key) -> tuple of (value, score: real)"));
    }

    private static async Task<Value> FuncZAdd(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zadd({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"zadd({arguments[1]}): incompatible operand, RedisValue expected");
        if (arguments[2] is RealValue score == false) throw new RuntimeException($"zadd({arguments[2]}): incompatible operand, real expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.SortedSetAddAsync(key.AsRedisKey(), value.AsRedisValue(), score.Value).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncZPopMin(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zpopmin({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var entry = await db.SortedSetPopAsync(key.AsRedisKey());
        return entry.HasValue
            ? TupleValue.Of(("value", new RedisValue(entry.Value.Element)), ("score", new RealValue(entry.Value.Score)))
            : NullValue.Instance;
    }

    private static async Task<Value> FuncZPopMax(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"zpopmax({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var entry = await db.SortedSetPopAsync(key.AsRedisKey(), Order.Descending);
        return entry.HasValue
            ? TupleValue.Of(("value", new RedisValue(entry.Value.Element)), ("score", new RealValue(entry.Value.Score)))
            : NullValue.Instance;
    }

    private static async Task<Value> FuncSAdd(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"sadd({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"sadd({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.SetAddAsync(key.AsRedisKey(), value.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncSRem(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"srem({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"srem({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.SetRemoveAsync(key.AsRedisKey(), value.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncLSet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"lset({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IntegerValue index == false) throw new RuntimeException($"lset({arguments[1]}): incompatible operand, int expected");
        if (arguments[2] is IRedisValue value == false) throw new RuntimeException($"lset({arguments[2]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        await db.ListSetByIndexAsync(key.AsRedisKey(), index.Value, value.AsRedisValue());
        return NullValue.Instance;
    }

    private static async Task<Value> FuncLPop(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"lpop({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var value = await db.ListLeftPopAsync(key.AsRedisKey());
        return new RedisValue(value);
    }

    private static async Task<Value> FuncRPop(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"rpop({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var value = await db.ListRightPopAsync(key.AsRedisKey());
        return new RedisValue(value);
    }

    private static async Task<Value> FuncLPush(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"lpush({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"lpush({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var length = await db.ListLeftPushAsync(key.AsRedisKey(), value.AsRedisValue());
        return IntegerValue.Of(length);
    }

    private static async Task<Value> FuncRPush(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"rpush({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"rpush({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var length = await db.ListRightPushAsync(key.AsRedisKey(), value.AsRedisValue());
        return IntegerValue.Of(length);
    }

    private static async Task<Value> FuncHDel(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hdel({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hdel({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.HashDeleteAsync(key.AsRedisKey(), field.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncHSet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"hset({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue field == false) throw new RuntimeException($"hset({arguments[1]}): incompatible operand, RedisValue expected");
        if (arguments[2] is IRedisValue value == false) throw new RuntimeException($"hset({arguments[2]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.HashSetAsync(key.AsRedisKey(), field.AsRedisValue(), value.AsRedisValue()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncSet(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"set({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisValue value == false) throw new RuntimeException($"set({arguments[1]}): incompatible operand, RedisValue expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.StringSetAsync(key.AsRedisKey(), value.AsRedisValue());
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncRename(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"rename({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is IRedisKey newKey == false) throw new RuntimeException($"rename({arguments[1]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.KeyRenameAsync(key.AsRedisKey(), newKey.AsRedisKey()).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncTtl(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"ttl({arguments[0]}): incompatible operand, RedisKey expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var timeSpan = await db.KeyTimeToLiveAsync(key.AsRedisKey());
        return timeSpan != null ? new DurationValue(timeSpan.Value) : NullValue.Instance;
    }

    private static async Task<Value> FuncExpire(Context ctx, Value[] arguments)
    {
        if (arguments[0] is IRedisKey key == false) throw new RuntimeException($"expire({arguments[0]}): incompatible operand, RedisKey expected");
        var duration = arguments[1] switch
        {
            NullValue _ => null,
            DurationValue d => d,
            _ => throw new RuntimeException($"expire({arguments[1]}): incompatible operand, Duration or null expected"),
        };
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var flag = await db.KeyExpireAsync(key.AsRedisKey(), duration?.Value).ConfigureAwait(false);
        return BoolValue.Of(flag);
    }

    private static async Task<Value> FuncDel(Context ctx, Value[] arguments)
    {
        var keys = arguments[0] switch
        {
            ListValue list => list.Cast<IRedisKey>().Select(r => r.AsRedisKey()).ToArray(),
            IRedisKey key => new[] {key.AsRedisKey()},
            _ => throw new RuntimeException($"del({arguments[0]}): incompatible operand, RedisKey or list of RedisKeys expected"),
        };
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var count = await db.KeyDeleteAsync(keys).ConfigureAwait(false);
        return IntegerValue.Of(count);
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

    private static async Task<Value> FuncBitPos(Context ctx, Value[] arguments)
    {
        if (arguments[0] is not IRedisKey key) throw new RuntimeException($"bitpos({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is not BoolValue bit) throw new RuntimeException($"bitpos({arguments[1]}): incompatible operand, Bool expected");
        if (arguments[2] is not IntegerValue start) throw new RuntimeException($"bitpos({arguments[2]}): incompatible operand, Integer expected");
        if (arguments[3] is not IntegerValue end) throw new RuntimeException($"bitpos({arguments[3]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var pos = await db.StringBitPositionAsync(key.AsRedisKey(), bit.Value, start.Value, end.Value);
        return IntegerValue.Of(pos);
    }

    private static async Task<Value> FuncBitCount(Context ctx, Value[] arguments)
    {
        if (arguments[0] is not IRedisKey key) throw new RuntimeException($"bitcount({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is not IntegerValue start) throw new RuntimeException($"bitcount({arguments[1]}): incompatible operand, Integer expected");
        if (arguments[2] is not IntegerValue end) throw new RuntimeException($"bitcount({arguments[2]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var count = await db.StringBitCountAsync(key.AsRedisKey(), start.Value, end.Value);
        return IntegerValue.Of(count);
    }

    private static async Task<Value> FuncGetBit(Context ctx, Value[] arguments)
    {
        if (arguments[0] is not IRedisKey key) throw new RuntimeException($"getbit({arguments[0]}): incompatible operand, RedisKey expected");
        if (arguments[1] is not IntegerValue offset) throw new RuntimeException($"getbit({arguments[1]}): incompatible operand, Integer expected");
        var db = await ctx.Redis.GetDatabase().ConfigureAwait(false);
        var bit = await db.StringGetBitAsync(key.AsRedisKey(), offset.Value);
        return BoolValue.Of(bit);
    }
}
