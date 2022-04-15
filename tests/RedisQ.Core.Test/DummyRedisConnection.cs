using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RedisQ.Core.Redis;
using StackExchange.Redis;

namespace RedisQ.Core.Test;

public class DummyRedisConnection : IRedisConnection
{
    public void Dispose()
    {}

    public Task<IDatabase> GetDatabase() => throw new NotSupportedException();
    public IAsyncEnumerable<RedisKey> ScanKeys(RedisValue pattern) => throw new NotSupportedException();
}
