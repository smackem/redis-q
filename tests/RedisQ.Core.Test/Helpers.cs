using System.Collections.Generic;
using System.Threading.Tasks;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;

namespace RedisQ.Core.Test;

internal static class Helpers
{
    public static readonly IRedisConnection DummyRedis = new DummyRedisConnection();
    public static readonly FunctionRegistry DefaultFunctions = new();
    
    public static async Task<IReadOnlyList<T>> Collect<T>(IAsyncEnumerable<T> enumerable)
    {
        var items = new List<T>();
        await foreach (var item in enumerable)
        {
            items.Add(item);
        }
        return items;
    }

}
