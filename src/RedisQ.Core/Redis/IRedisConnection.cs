using StackExchange.Redis;

namespace RedisQ.Core.Redis;

public interface IRedisConnection : IDisposable
{
    Task<IDatabase> GetDatabase();
    IAsyncEnumerable<RedisKey> ScanKeys(RedisValue pattern);
}
