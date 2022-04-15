using System.Collections.Generic;
using System.Threading.Tasks;
using RedisQ.Core.Runtime;
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
        Assert.IsType<VectorValue>(value);
        var keys = await Collect((VectorValue) value);
        Assert.Collection(keys,
            v => Assert.True(v is KeyValue { Value: "test-key-1" }));
    }

    private static async Task<IReadOnlyList<T>> Collect<T>(IAsyncEnumerable<T> enumerable)
    {
        var items = new List<T>();
        await foreach (var item in enumerable)
        {
            items.Add(item);
        }
        return items;
    }
}