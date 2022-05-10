using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;

namespace RedisQ.Core.Test;

internal static class Helpers
{
    public static readonly IRedisConnection DummyRedis = new DummyRedisConnection();
    public static readonly FunctionRegistry DefaultFunctions = new(true);
}
