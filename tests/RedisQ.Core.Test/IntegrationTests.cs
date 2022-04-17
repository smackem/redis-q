using System.Linq;
using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using StackExchange.Redis;
using Xunit;
using RedisValue=RedisQ.Core.Runtime.RedisValue;

namespace RedisQ.Core.Test;

public class IntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task FromKeysWhere()
    {
        const string source = @"
from k in keys(""*"")
where strlen(k) > 3
select get(k)
";
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "a");
        db.StringSet("key-2", "ab");
        db.StringSet("key-3", "abc");
        db.StringSet("key-4", "abcd");
        db.StringSet("key-5", "abcde");
        var value = await Interpret(source, redis);
        Assert.True(value is EnumerableValue, "value is not enumerable");
        var coll = await ((EnumerableValue)value).Collect();
        Assert.Equal(2, coll.Count);
        Assert.Contains(coll, v => Equals(v, new RedisValue("abcd")));
        Assert.Contains(coll, v => Equals(v, new RedisValue("abcde")));
    }

    [Fact]
    public async Task FromKeysWhereHashVal()
    {
        const string source = @"
from k in keys(""hash-*"")
let type = hget(k, ""type"")
where type == ""some""
select (k, hget(k, ""name""))
";
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.HashSet("hash-1", new HashEntry[] { new("name", "bob"), new("type", "none") });
        db.HashSet("hash-2", new HashEntry[] { new("name", "alice"), new("type", "some") });
        db.HashSet("hash-3", new HashEntry[] { new("name", "george"), new("type", "some") });
        db.HashSet("hash-4", new HashEntry[] { new("name", "arthur"), new("type", "none") });
        db.HashSet("hash-5", new HashEntry[] { new("name", "mary"), new("type", "some") });
        db.HashSet("hash-6", new HashEntry[] { new("name", "anne"), new("type", "none") });
        var value = await Interpret(source, redis);
        Assert.True(value is EnumerableValue, "value is not enumerable");
        var coll = await ((EnumerableValue)value).Collect();
        Assert.Equal(3, coll.Count);
        Assert.Contains(coll, v => Equals(v, TupleValue.Of(new RedisKeyValue("hash-2"), new RedisValue("alice"))));
        Assert.Contains(coll, v => Equals(v, TupleValue.Of(new RedisKeyValue("hash-3"), new RedisValue("george"))));
        Assert.Contains(coll, v => Equals(v, TupleValue.Of(new RedisKeyValue("hash-5"), new RedisValue("mary"))));
    }
}
