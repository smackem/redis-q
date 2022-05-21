
using StackExchange.Redis;

namespace RedisQ.Core.Redis;

public class RedisConnection : IRedisConnection
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly string _connectionString;

    // ReSharper disable once ArrangeModifiersOrder
    private volatile ConnectionMultiplexer? _redis;

    public RedisConnection(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Dispose()
    {
        _redis?.Dispose();
        GC.SuppressFinalize(this);
    }
    
    public async Task<IDatabase> GetDatabase()
    {
        var redis = await Connect();
        return redis.GetDatabase();
    }

    public async IAsyncEnumerable<RedisKey> ScanKeys(RedisValue pattern)
    {
        var redis = await Connect();
        var endPoints = redis.GetEndPoints();

        foreach (var endPoint in endPoints)
        {
            var server = redis.GetServer(endPoint);

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                yield return key;
            }
        }
    }

    private async Task<ConnectionMultiplexer> Connect()
    {
        // ReSharper disable once InvertIf
        if (_redis == null)
        {
            try
            {
                await _semaphore.WaitAsync();

                // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
                if (_redis == null)
                {
                    _redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
                }
            }
            finally
            {
                _semaphore.Release(1);
            }
        }

        return _redis;
    }
}
