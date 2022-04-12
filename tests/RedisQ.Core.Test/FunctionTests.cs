using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using RedisQ.Core.Redis;
using RedisQ.Core.Runtime;
using Xunit;

namespace RedisQ.Core.Test;

public class FunctionTests : IDisposable
{
    private readonly Process _redisProcess;
    private const int Port = 55666;

    public FunctionTests()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "redis-server",
            Arguments = $"--port {Port} --save \"\" --appendonly no",
        };
        _redisProcess = Process.Start(psi)!;
    }
    
    [Fact]
    public async void Keys()
    {
        using var redis = new RedisConnection($"localhost:{Port}");
        var db = await redis.GetDatabase();
        db.StringSet("test-key-1", "test-value");
        var functions = new FunctionRegistry();
        var ctx = Context.Root(redis, functions);
        var keysFunc = functions.Lookup("keys", 1);
        var args = new Value[] { new StringValue("*") };
        var result = await keysFunc.Invoke(ctx, args);
        Assert.IsType<VectorValue>(result);
        var keys = await Collect((VectorValue) result);
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _redisProcess.Kill();
        _redisProcess.WaitForExit(1000);
        _redisProcess.Dispose();
    }
}