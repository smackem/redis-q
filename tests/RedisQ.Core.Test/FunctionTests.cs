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
}
