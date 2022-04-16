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
}