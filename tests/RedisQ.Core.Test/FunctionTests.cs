using System.Threading.Tasks;
using RedisQ.Core.Runtime;
using StackExchange.Redis;
using Xunit;

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
}