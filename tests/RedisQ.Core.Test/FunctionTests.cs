using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using Xunit;
using SR = StackExchange.Redis;

namespace RedisQ.Core.Test;

public class FunctionTests : IntegrationTestBase
{
    [Fact]
    public async Task Keys()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("test-key-1", "test-value");
        var expr = Compile("keys(\"*\")");
        var value = await Eval(expr, redis);
        Assert.IsType<EnumerableValue>(value);
        var keys = await ((EnumerableValue) value).Collect();
        Assert.Collection(keys,
            v => Assert.True(v is RedisKeyValue key && key.Value == "test-key-1"));
    }

    [Fact]
    public async Task Get()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("test-key", "test-value");
        var expr = Compile(@"get(""test-key"")");
        var value = await Eval(expr, redis);
        Assert.IsType<RedisValue>(value);
        Assert.Equal(new RedisValue("test-value"), value);
    }

    [Fact]
    public async Task GetEmpty()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("test-key", "test-value");
        var expr = Compile(@"get(""test-key-non-existing"")");
        var value = await Eval(expr, redis);
        Assert.IsType<RedisValue>(value);
        Assert.Equal(RedisValue.Empty, value);
    }

    [Fact]
    public async Task MGet()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "value-1");
        db.StringSet("key-2", "value-2");
        db.StringSet("key-3", "value-3");
        var expr = Compile(@"mget([""key-1"", ""key-2"", ""key-3""])");
        var value = await Eval(expr, redis);
        Assert.IsType<ListValue>(value);
        Assert.Collection((ListValue)value,
            v => Assert.Equal(new RedisValue("value-1"), v),
            v => Assert.Equal(new RedisValue("value-2"), v),
            v => Assert.Equal(new RedisValue("value-3"), v));
    }

    [Fact]
    public async Task MGetPartial()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "value-1");
        db.StringSet("key-2", "value-2");
        db.StringSet("key-3", "value-3");
        var expr = Compile(@"mget([""key-1-x"", ""key-2"", ""key-3-x""])");
        var value = await Eval(expr, redis);
        Assert.IsType<ListValue>(value);
        Assert.Collection((ListValue)value,
            v => Assert.Equal(RedisValue.Empty, v),
            v => Assert.Equal(new RedisValue("value-2"), v),
            v => Assert.Equal(RedisValue.Empty, v));
    }

    [Fact]
    public async Task StrLen()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "1234567890");
        var expr = Compile(@"strlen(""key-1"")");
        var value = await Eval(expr, redis);
        Assert.Equal(IntegerValue.Of(10), value);
    }

    [Fact]
    public async Task StrLenEmpty()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "1234567890");
        var expr = Compile(@"strlen(""key-1-x"")");
        var value = await Eval(expr, redis);
        Assert.Equal(IntegerValue.Zero, value);
    }

    [Fact]
    public async Task GetRange()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.StringSet("key-1", "1234567890");
        var expr1 = Compile(@"getrange(""key-1"", 0, 3)");
        var value1 = await Eval(expr1, redis);
        Assert.Equal(new RedisValue("1234"), value1);
        var expr2 = Compile(@"getrange(""key-1"", 0, -1)");
        var value2 = await Eval(expr2, redis);
        Assert.Equal(new RedisValue("1234567890"), value2);
    }

    [Fact]
    public async Task HKeys()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.HashSet("person", "name", "bob");
        db.HashSet("person", "age", 60);
        var expr1 = Compile(@"hkeys(""person"")");
        var value1 = await Eval(expr1, redis);
        Assert.IsType<ListValue>(value1);
        Assert.Collection((ListValue)value1,
            v => Assert.Equal(new RedisValue("name"), v),
            v => Assert.Equal(new RedisValue("age"), v));
    }

    [Fact]
    public async Task HGet()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.HashSet("person", "name", "bob");
        db.HashSet("person", "age", 60);
        var expr1 = Compile(@"hget(""person"", ""name"")");
        var value1 = await Eval(expr1, redis);
        Assert.Equal(new RedisValue("bob"), value1);
        var expr2 = Compile(@"hget(""person"", ""nonexisting"")");
        var value2 = await Eval(expr2, redis);
        Assert.Equal(RedisValue.Empty, value2);
    }

    [Fact]
    public async Task HGetAll()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.HashSet("person", "name", "bob");
        db.HashSet("person", "age", 60);
        var expr1 = Compile(@"hgetall(""person"")");
        var value1 = await Eval(expr1, redis);
        Assert.IsType<ListValue>(value1);
        Assert.Collection((ListValue) value1,
            v => Assert.Equal(TupleValue.Of(new RedisValue("name"), new RedisValue("bob")), v),
            v => Assert.Equal(TupleValue.Of(new RedisValue("age"), new RedisValue(60)), v));
    }

    [Fact]
    public async Task LLen()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.ListRightPush("list", "item-1");
        db.ListRightPush("list", "item-2");
        db.ListRightPush("list", "item-3");
        var expr1 = Compile(@"llen(""list"")");
        var value1 = await Eval(expr1, redis);
        Assert.Equal(IntegerValue.Of(3), value1);
    }

    [Fact]
    public async Task LRange()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.ListRightPush("list", "item-1");
        db.ListRightPush("list", "item-2");
        db.ListRightPush("list", "item-3");
        db.ListRightPush("list", "item-4");
        db.ListRightPush("list", "item-5");
        var expr1 = Compile(@"lrange(""list"", 0, -1)");
        var value1 = await Eval(expr1, redis);
        Assert.IsType<ListValue>(value1);
        Assert.Collection((ListValue)value1,
            v => Assert.Equal(new RedisValue("item-1"), v),
            v => Assert.Equal(new RedisValue("item-2"), v),
            v => Assert.Equal(new RedisValue("item-3"), v),
            v => Assert.Equal(new RedisValue("item-4"), v),
            v => Assert.Equal(new RedisValue("item-5"), v));
        var expr2 = Compile(@"lrange(""list"", 1, 2)");
        var value2 = await Eval(expr2, redis);
        Assert.IsType<ListValue>(value2);
        Assert.Collection((ListValue)value2,
            v => Assert.Equal(new RedisValue("item-2"), v),
            v => Assert.Equal(new RedisValue("item-3"), v));
    }

    [Fact]
    public async Task LIndex()
    {
        using var redis = Connect();
        var db = await redis.GetDatabase();
        db.ListRightPush("list", "item-1");
        db.ListRightPush("list", "item-2");
        db.ListRightPush("list", "item-3");
        db.ListRightPush("list", "item-4");
        db.ListRightPush("list", "item-5");
        var expr1 = Compile(@"lindex(""list"", 0)");
        var value1 = await Eval(expr1, redis);
        Assert.Equal(new RedisValue("item-1"), value1);
        var expr2 = Compile(@"lindex(""list"", -1)");
        var value2 = await Eval(expr2, redis);
        Assert.Equal(new RedisValue("item-5"), value2);
        var expr3 = Compile(@"lindex(""list"", 1000)");
        var value3 = await Eval(expr3, redis);
        Assert.Equal(RedisValue.Empty, value3);
    }
}
